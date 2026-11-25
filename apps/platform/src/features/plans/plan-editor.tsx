"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  AuditRetention,
  createPlan,
  ErrorCode,
  FieldErrorCode,
  getGetMetersQueryOptions,
  getGetPlanQueryKey,
  getGetPlanQueryOptions,
  getGetPriceChangeQueryOptions,
  getListPlansQueryOptions,
  getPlanUpdatePreview,
  MeterSettingRequestOverage,
  type PlanResponse,
  type PlanUpdatePreviewPageResponse,
  ProblemDetailsError,
  problemOf,
  savedUnderKey,
  schemas,
  type UpdatePlanRequest,
  updatePlan,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { ArrowLeftIcon, Icon } from "@repo/ui/components/icon";
import { Label } from "@repo/ui/components/label";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { buttonVariants } from "@repo/ui/components/ui/button";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { Input as InputBase } from "@repo/ui/components/ui/input";
import { formatMoney, parseMoney } from "@repo/ui/lib/format-money";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useFreshRouter } from "@repo/ui/lib/use-fresh-router";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { type ReactNode, useEffect, useId, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import {
  belowFloor,
  type Draft,
  draftOf,
  entitlementAreas,
  entitlementKeys,
  featureOf,
  isWholeNumber,
  lowers,
  requestOf,
  retentionKey,
  useEntitlementText,
  valueFrom,
  waitsForFeature,
} from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";
import { NotFoundPage } from "@/shared/not-found-page";
import { planPage, plansPage, plansQuery, refreshPlans } from "@/shared/plans";
import { ProblemScreen } from "@/shared/problem-screen";
import { afterAccountRefusal } from "@/shared/refusal";
import { ValueField } from "@/shared/value-field";
import { ImpactDialog } from "./impact-dialog";
import { OldPriceBanner } from "./old-price-banner";
import {
  addedMeter,
  freePrices,
  type MeterDraft,
  MetersArea,
  meterDraftsOf,
  meterKeysOf,
  PricesArea,
  type PriceTexts,
  priceKeys,
  priceTextsOf,
} from "./plan-prices";
import { PriceChangeDialog } from "./price-change-dialog";

// The generated schema of UpdatePlan, which CreatePlan shares (F11): the API trims the name and needs 1 to 100 characters; the
// generated schema has the limit only, so the form adds the trim and the minimum. The values are one draft per key of the list,
// in its order, because a key's dots would nest a field's path. The Activity log's period has a floor (6.11).
const editing = schemas.UpdatePlanBody.omit({
  entitlements: true,
  prices: true,
  meters: true,
}).extend({
  prices: z.object({
    baseMonthly: z.string(),
    baseYearly: z.string(),
    perPersonMonthly: z.string(),
    perPersonYearly: z.string(),
  }),
  meters: z.array(
    z.object({
      key: z.string(),
      quota: z.string(),
      overage: z.enum([
        MeterSettingRequestOverage.Stop,
        MeterSettingRequestOverage.Charge,
      ]),
      unitPrice: z.string(),
      unitSize: z.number(),
    }),
  ),
  name: z.string().trim().min(1).max(schemas.updatePlanBodyNameMax),
  drafts: z
    .array(
      z.object({
        enabled: z.boolean().nullable(),
        text: z.string().refine(isWholeNumber),
        unlimited: z.boolean(),
      }),
    )
    .superRefine((drafts, context) => {
      drafts.forEach((draft, index) => {
        if (belowFloor(entitlementKeys[index]?.key ?? "", draft))
          context.addIssue({
            code: "custom",
            path: [index, "text"],
            message: "floor",
          });
      });
    }),
});
type Editing = z.infer<typeof editing>;

/**
 * The values of a plan as the form starts them: one draft per key, empty where the plan has no value. A new plan starts empty but for
 * the Activity log, which starts at the default period (6.10).
 */
const draftsOf = (plan: PlanResponse | null): Draft[] =>
  entitlementKeys.map(({ key }) =>
    draftOf(
      plan
        ? (plan.entitlements[key] ?? null)
        : key === retentionKey
          ? { enabled: null, maximum: AuditRetention.Default, unlimited: false }
          : null,
    ),
  );

/**
 * Exactly the values on screen (the API's UpdatePlan: a key left out is closed). Every key of the list is here, from the generated
 * client, so no key is left out by accident: a key goes out with its value, or stays out because the screen shows it unset.
 */
const bodyOf = (form: Editing, currency: string | null): UpdatePlanRequest => ({
  name: form.name,
  isDefault: form.isDefault,
  ...(currency === null ? {} : moneyBodyOf(form, currency)),
  entitlements: Object.fromEntries(
    entitlementKeys.flatMap(({ key, isFeature }, index) => {
      const draft = form.drafts[index];
      const value = draft ? valueFrom(isFeature, draft) : null;
      return value ? [[key, requestOf(value)]] : [];
    }),
  ),
});

/** A plan's prices and meters as the form starts them; a new plan is free and has no meter, as CreatePlan's default. */
const moneyOf = (
  plan: PlanResponse | null,
  currency: string,
): { prices: PriceTexts; meters: MeterDraft[] } => ({
  prices: priceTextsOf(plan?.prices ?? freePrices, currency),
  meters: meterDraftsOf(plan?.meters ?? [], currency),
});

type MoneyErrors = {
  any: boolean;
  prices: Partial<Record<(typeof priceKeys)[number], boolean>>;
  meters: Partial<Record<number, { quota?: boolean; unitPrice?: boolean }>>;
};
const noMoneyErrors: MoneyErrors = { any: false, prices: {}, meters: {} };

/** The price and meter fields that are no amount or no whole number, before a save (`plan.price_invalid`, `plan.meter_invalid`). */
const moneyErrors = (form: Editing, currency: string): MoneyErrors => {
  const prices = Object.fromEntries(
    priceKeys
      .filter((key) => parseMoney(form.prices[key], currency) === null)
      .map((key) => [key, true]),
  );
  const meters = Object.fromEntries(
    form.meters.flatMap((meter, index) => {
      const quota = !isWholeNumber(meter.quota) || meter.quota.trim() === "";
      const unitPrice = parseMoney(meter.unitPrice, currency) === null;
      return quota || unitPrice ? [[index, { quota, unitPrice }]] : [];
    }),
  );
  return {
    any: Object.keys(prices).length + Object.keys(meters).length > 0,
    prices,
    meters,
  };
};

/** The prices and meters of a save. The fields are checked before (`moneyErrors`): each parses here. */
const moneyBodyOf = (
  form: Editing,
  currency: string,
): Pick<UpdatePlanRequest, "prices" | "meters"> => ({
  prices: {
    baseMonthly: parseMoney(form.prices.baseMonthly, currency) ?? 0,
    baseYearly: parseMoney(form.prices.baseYearly, currency) ?? 0,
    perPersonMonthly: parseMoney(form.prices.perPersonMonthly, currency) ?? 0,
    perPersonYearly: parseMoney(form.prices.perPersonYearly, currency) ?? 0,
  },
  meters: form.meters.map((meter) => ({
    key: meter.key,
    quota: Number(meter.quota),
    overage: meter.overage,
    unitPrice: parseMoney(meter.unitPrice, currency) ?? 0,
    unitSize: meter.unitSize,
  })),
});

/** Whether a save takes something away from the plan's organisations, so the impact preview runs first (the PlanImpact board). */
/** The save changes a price of the plan (B13). */
const pricesChange = (plan: PlanResponse, body: UpdatePlanRequest) =>
  body.prices !== null &&
  body.prices !== undefined &&
  (Object.keys(plan.prices) as (keyof PlanResponse["prices"])[]).some(
    (key) => body.prices?.[key] !== plan.prices[key],
  );

const takesAway = (plan: PlanResponse, form: Editing) =>
  entitlementKeys.some(({ key, isFeature }, index) => {
    const draft = form.drafts[index];
    return (
      draft !== undefined &&
      lowers(
        isFeature,
        plan.entitlements[key] ?? null,
        valueFrom(isFeature, draft),
      )
    );
  });

/**
 * A plan (6.04 to 6.09, 6.18) and New plan (6.10 to 6.12) with `planId` null: Back to Plans, the title and how many organisations are
 * on the plan, then the keys grouped by area, each a row of a card. A feature is a switch, a limit a whole number or Unlimited, and a
 * key with no value is closed. Save changes saves only a change and keeps the person on the plan in its saved state (6.08); a new
 * plan opens on its own page (6.12). The default plan keeps its mark until another plan takes it. No delete. On a phone a plan opens
 * as the summary of its values, and Edit plan shows the editor (6.18).
 */
export function PlanEditor({ planId }: { planId: string | null }) {
  const known =
    planId === null ||
    schemas.GetPlanParams.shape.planId.safeParse(planId).success;
  const plan = useQuery(
    getGetPlanQueryOptions(planId ?? "", {
      query: { enabled: known && planId !== null },
    }),
  );
  const t = useTranslations();
  const locale = useLocale();
  const [editing, setEditing] = useState(false);
  if (!known || problemOf(plan.error)?.code === ErrorCode.PlanNotFound)
    return <NotFoundPage />;
  if (plan.error) return <ProblemScreen error={plan.error} />;
  if (planId !== null && !plan.data)
    return (
      <Frame>
        <Skeleton rows={4} label={t("plans.section")} />
      </Frame>
    );
  const saved = plan.data ?? null;
  return (
    <Frame>
      <div className="flex flex-col gap-0.5">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-page-title">
            <bdi>{saved ? saved.name : t("plans.createTitle")}</bdi>
          </h1>
          {saved?.isDefault && <Label>{t("plans.default")}</Label>}
        </div>
        <p className="text-body text-muted-foreground">
          {saved
            ? `${t("plans.organisationCount", { count: saved.tenantCount })} ${t(
                "plans.pricesLine",
                {
                  base: formatMoney(
                    saved.prices.baseMonthly,
                    saved.currency,
                    locale,
                  ),
                  perPerson: formatMoney(
                    saved.prices.perPersonMonthly,
                    saved.currency,
                    locale,
                  ),
                },
              )}`
            : t("plans.createText")}
        </p>
      </div>
      {saved && <OldPriceBanner plan={saved} />}
      {saved && !editing && (
        <Summary plan={saved} onEdit={() => setEditing(true)} />
      )}
      <div className={saved && !editing ? "max-tablet:hidden" : undefined}>
        <Form key={saved?.id ?? "new"} plan={saved} />
      </div>
    </Frame>
  );
}

/** The page: Back to Plans above the content (6.04). */
function Frame({ children }: { children: ReactNode }) {
  const t = useTranslations();
  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <div>
        {/* A link to the list, drawn as the ghost button of the frame: a link, not a button, because it navigates. */}
        <Link href={plansPage} className={buttonVariants({ variant: "ghost" })}>
          <Icon icon={ArrowLeftIcon} size={16} className="rtl:rotate-180" />
          {t("plans.title")}
        </Link>
      </div>
      {children}
    </section>
  );
}

/** A plan's values as a phone shows them (6.18): each key's name over its value, and Edit plan. Hidden from bp-tablet. */
function Summary({ plan, onEdit }: { plan: PlanResponse; onEdit: () => void }) {
  const t = useTranslations();
  const text = useEntitlementText();
  const headingId = useId();
  return (
    <div className="flex flex-col gap-3 tablet:hidden">
      <section
        aria-labelledby={headingId}
        className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4"
      >
        <p id={headingId} className="text-body font-medium">
          {t("plans.limits")}
        </p>
        <dl className="flex flex-col gap-3">
          {entitlementAreas().flatMap(({ keys }) =>
            keys.map(({ key, isFeature }) => (
              <div key={key} className="flex flex-col">
                <dt className="text-small text-muted-foreground">
                  {text.name(key)}
                </dt>
                <dd className="text-body">
                  {waitsForFeature(
                    key,
                    (feature) => plan.entitlements[feature] ?? null,
                  )
                    ? text.off()
                    : text.label(
                        isFeature,
                        plan.entitlements[key] ?? null,
                        key,
                      )}
                </dd>
              </div>
            )),
          )}
        </dl>
      </section>
      <Button size="lg" onClick={onEdit}>
        {t("plans.edit")}
      </Button>
    </div>
  );
}

/** One row of a card (6.04): the label in a 300 px column, the control after it; one above the other on a phone. */
function Row({
  label,
  description,
  labelId,
  children,
  group = false,
}: {
  label: ReactNode;
  description?: string;
  labelId: string;
  children: ReactNode;
  /** A key's row: a group named by its label, which holds the switch or the number and Unlimited. */
  group?: boolean;
}) {
  const className =
    "flex flex-col gap-3 px-5 py-3 tablet:flex-row tablet:items-center tablet:gap-6";
  const content = (
    <>
      <div className="flex min-w-0 flex-col gap-0.5 tablet:w-75 tablet:shrink-0">
        <span id={labelId} className="text-body font-medium">
          {label}
        </span>
        {description && (
          <p className="text-small text-muted-foreground">{description}</p>
        )}
      </div>
      <div className="min-w-0 flex-1">{children}</div>
    </>
  );
  if (!group) return <div className={className}>{content}</div>;
  return (
    // biome-ignore lint/a11y/useSemanticElements: a fieldset's legend would draw a frame the card does not have; the group names the row.
    <div role="group" aria-labelledby={labelId} className={className}>
      {content}
    </div>
  );
}

/** An area of the plan (6.04): its name over a card of rows. */
function Area({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-body font-medium">{title}</h2>
      <div className="flex flex-col divide-y divide-border rounded-lg border border-border bg-card">
        {children}
      </div>
    </section>
  );
}

function Form({ plan }: { plan: PlanResponse | null }) {
  const t = useTranslations();
  const text = useEntitlementText();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const router = useFreshRouter(useRouter());
  const { key, renew } = useIntentKey();
  const markHint = useId();
  const plans = useQuery(getListPlansQueryOptions(plansQuery));
  const currentDefault = plans.data?.items.find((item) => item.isDefault);
  // The deployment has one currency (40a); a new plan reads it from the default plan, as no plan of its own answers it yet.
  const defaultPlan = useQuery(
    getGetPlanQueryOptions(currentDefault?.id ?? "", {
      query: { enabled: plan === null && currentDefault !== undefined },
    }),
  );
  const currency = plan?.currency ?? defaultPlan.data?.currency ?? null;
  const [priceAsk, setPriceAsk] = useState<{
    body: UpdatePlanRequest;
    takes: boolean;
  } | null>(null);
  // While billing is off the API answers 404 billing.off: nobody pays, so no save asks (6.14).
  const priceChange = useQuery({
    ...getGetPriceChangeQueryOptions(plan?.id ?? ""),
    enabled: plan !== null,
    retry: false,
  });
  const [impact, setImpact] = useState<{
    body: UpdatePlanRequest;
    first: PlanUpdatePreviewPageResponse;
  } | null>(null);
  const form = useForm<Editing>({
    resolver: zodResolver(editing),
    defaultValues: {
      name: plan?.name ?? "",
      isDefault: plan?.isDefault ?? false,
      drafts: draftsOf(plan),
      ...moneyOf(plan, plan?.currency ?? "EUR"),
    },
  });
  // Another value of any field is another intent of CreatePlan: it gets a new key (F48).
  useEffect(
    () => form.subscribe({ formState: { values: true }, callback: renew }),
    [form, renew],
  );
  const drafts = form.watch("drafts");
  const prices = form.watch("prices");
  const meters = form.watch("meters");
  // Every meter of the code (X5): a meter the plan does not set yet gets a row too (6.26).
  const code = useQuery(getGetMetersQueryOptions());
  const [wrongMoney, setWrongMoney] = useState<MoneyErrors>(noMoneyErrors);
  const isDefault = form.watch("isDefault");
  const locked = plan?.isDefault === true;

  const done = (saved: PlanResponse, created: boolean) => {
    renew();
    setImpact(null);
    queryClient.setQueryData(getGetPlanQueryKey(saved.id), saved);
    void refreshPlans(queryClient, saved.id);
    const description = created
      ? t("plans.createdText")
      : t("plans.savedText", { count: saved.tenantCount });
    showToast({
      title: t.rich(created ? "plans.created" : "plans.saved", {
        name: saved.name,
        ...isolated,
      }),
      description: description || undefined,
    });
    // A new plan opens on its own page (6.12); a saved plan stays on screen in its saved state (6.08).
    if (created) void router.push(planPage(saved.id));
    else
      form.reset({
        name: saved.name,
        isDefault: saved.isDefault,
        drafts: draftsOf(saved),
        ...moneyOf(saved, saved.currency),
      });
  };

  const refused = (error: unknown) => {
    const code = afterAccountRefusal(queryClient, error);
    if (savedUnderKey(error)) renew();
    setImpact(null);
    if (code === ErrorCode.PlanNameTaken)
      form.setError("name", { message: "nameTaken" });
    if (code === ErrorCode.PlanNameInvalid)
      form.setError("name", { message: FieldErrorCode.Required });
    // A value the API refuses shows under its key: a wrong value, or a limit changed while its feature is off (37d).
    if (
      code === ErrorCode.PlanEntitlementInvalid ||
      code === ErrorCode.PlanFeatureRequired
    ) {
      const index = entitlementKeys.findIndex(
        (item) =>
          error instanceof ProblemDetailsError &&
          item.key === error.problem?.entitlement,
      );
      if (index >= 0)
        form.setError(`drafts.${index}.text`, {
          message:
            code === ErrorCode.PlanFeatureRequired
              ? "featureRequired"
              : "invalid",
        });
    }
    const [nameCode] =
      (error instanceof ProblemDetailsError && error.problem?.errors?.name) ||
      [];
    if (nameCode) form.setError("name", { message: nameCode });
    // The plan or the default mark changed meanwhile: the screen reads both again (F47).
    if (
      code === ErrorCode.PlanDefaultRequired ||
      code === ErrorCode.PlanNotFound
    )
      void refreshPlans(queryClient, plan?.id);
  };

  const create = useMutation({
    mutationFn: ({ body, key }: { body: UpdatePlanRequest; key: string }) =>
      createPlan(body, { "Idempotency-Key": key }),
    onSuccess: (saved) => done(saved, true),
    onError: refused,
  });
  const update = useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdatePlanRequest }) =>
      updatePlan(id, body),
    onSuccess: (saved) => done(saved, false),
    onError: refused,
  });
  const preview = useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdatePlanRequest }) =>
      getPlanUpdatePreview(id, { entitlements: body.entitlements }),
    // A shorter Activity log that no organisation follows (each has an override, #271) shortens nothing: no dialog.
    onSuccess: (first, { id, body }) =>
      first.total === 0 && !(first.retentionShortened?.count ?? 0)
        ? update.mutate({ id, body })
        : setImpact({ body, first }),
    onError: refused,
  });
  const pending = create.isPending || update.isPending || preview.isPending;

  const submit = form.handleSubmit((values) => {
    if (pending) return;
    // Until the currency is known the prices and meters do not show, and a save leaves them out: the API keeps them (40a).
    const wrong =
      currency === null ? noMoneyErrors : moneyErrors(values, currency);
    setWrongMoney(wrong);
    if (wrong.any) return;
    const body = bodyOf(values, currency);
    if (!plan) create.mutate({ body, key });
    // New prices of a plan that someone pays for ask who pays them, and when (6.14), before the save goes on.
    else if (pricesChange(plan, body) && (priceChange.data?.payers ?? 0) > 0)
      setPriceAsk({ body, takes: takesAway(plan, values) });
    else saveOn(plan, body, takesAway(plan, values));
  });
  const saveOn = (
    saving: PlanResponse,
    body: UpdatePlanRequest,
    takes: boolean,
  ) =>
    takes
      ? preview.mutate({ id: saving.id, body })
      : update.mutate({ id: saving.id, body });

  const failure = (() => {
    const error = create.error ?? update.error ?? preview.error;
    const problem = problemOf(error);
    const code = problem?.code;
    if (
      !error ||
      code === ErrorCode.PlanNameTaken ||
      code === ErrorCode.PlanNameInvalid ||
      code === ErrorCode.PlanEntitlementInvalid ||
      code === ErrorCode.PlanFeatureRequired ||
      code === ErrorCode.RequestValidationFailed
    )
      return null;
    // A code that says the plan changed meanwhile has its own text; the screen has read again.
    const known =
      code === ErrorCode.PlanDefaultRequired || code === ErrorCode.PlanNotFound;
    return known ? t(`errors.${code}`) : failureText(problem);
  })();

  const errors = form.formState.errors;
  const nameError = (() => {
    const message = errors.name?.message;
    if (!message) return undefined;
    if (message === "nameTaken") return t("plans.nameTaken");
    if (message === FieldErrorCode.TooLong || message === "validation.too_long")
      return t("plans.nameTooLong", { max: schemas.updatePlanBodyNameMax });
    return t("plans.nameRequired");
  })();
  // After a save a value is checked again as it is typed, as the name is (6.11).
  // A feature's change also clears the refusal of the limits that depend on it (37d).
  const setDraft = (index: number, draft: Draft) => {
    form.clearErrors(`drafts.${index}.text`);
    entitlementKeys.forEach((item, other) => {
      if (featureOf(item.key) === entitlementKeys[index]?.key)
        form.clearErrors(`drafts.${other}.text`);
    });
    form.setValue(`drafts.${index}`, draft, { shouldDirty: true });
    if (form.formState.isSubmitted) void form.trigger(`drafts.${index}.text`);
  };
  const markText = locked
    ? t("plans.defaultLocked")
    : isDefault
      ? t("plans.defaultOn")
      : t.rich("plans.defaultOff", {
          plan: currentDefault?.name ?? "",
          ...isolated,
        });

  const nameId = `${markHint}-name`;
  const nameNote = `${markHint}-name-note`;
  return (
    <form
      noValidate
      onSubmit={(event) => void submit(event)}
      className="flex flex-col gap-4"
    >
      <Area title={t("plans.section")}>
        <Row
          labelId={`${nameId}-label`}
          label={<label htmlFor={nameId}>{t("plans.name")}</label>}
        >
          <div className="flex flex-col gap-1.5">
            <InputBase
              id={nameId}
              autoComplete="off"
              aria-invalid={nameError ? true : undefined}
              aria-describedby={nameError ? nameNote : undefined}
              className="h-control-md w-full pointer-coarse:h-control-lg tablet:w-80"
              {...form.register("name")}
            />
            {nameError && (
              <p id={nameNote} className="text-small text-destructive-text">
                {nameError}
              </p>
            )}
          </div>
        </Row>
        <Row
          labelId={`${markHint}-label`}
          label={
            <label htmlFor={`${markHint}-box`}>{t("plans.defaultPlan")}</label>
          }
        >
          <div className="flex items-start gap-2">
            <Checkbox
              id={`${markHint}-box`}
              className="mt-0.5"
              aria-describedby={markHint}
              checked={isDefault}
              disabled={locked}
              onCheckedChange={(checked) =>
                form.setValue("isDefault", checked, { shouldDirty: true })
              }
            />
            <p
              id={markHint}
              className="max-w-105 text-small text-muted-foreground"
            >
              {markText}
            </p>
          </div>
        </Row>
      </Area>
      {entitlementAreas().map(({ area, keys }) => (
        <Area key={area} title={text.area(area)}>
          {keys.map(({ key, isFeature }) => {
            const index = entitlementKeys.findIndex((item) => item.key === key);
            const draft = drafts[index];
            if (!draft) return null;
            // A limit whose feature is off in this form keeps its value, disabled, until the feature is turned on (37d).
            const waits = waitsForFeature(key, (feature) => {
              const other =
                drafts[
                  entitlementKeys.findIndex((item) => item.key === feature)
                ];
              return other ? valueFrom(true, other) : null;
            });
            const message = errors.drafts?.[index]?.text?.message;
            return (
              <Row
                key={key}
                group
                labelId={`${markHint}-${index}`}
                label={text.row(key)}
                description={text.description(key)}
              >
                <ValueField
                  isFeature={isFeature}
                  label={text.row(key)}
                  draft={draft}
                  onChange={(next) => setDraft(index, next)}
                  error={
                    !message
                      ? undefined
                      : message === "featureRequired"
                        ? text.requiresFeature(key)
                        : key === retentionKey
                          ? text.floor()
                          : t("plans.numberInvalid")
                  }
                  unit={text.unit(key, draft.text)}
                  disabled={waits}
                  note={waits ? text.requiresFeature(key) : undefined}
                  showUnset
                />
              </Row>
            );
          })}
        </Area>
      ))}
      {currency !== null && (
        <>
          <PricesArea
            texts={prices}
            saved={plan?.prices ?? null}
            currency={currency}
            errors={wrongMoney.prices}
            onChange={(key, text) =>
              form.setValue(`prices.${key}`, text, { shouldDirty: true })
            }
          />
          <MetersArea
            keys={meterKeysOf(
              code.data?.meters.map((meter) => meter.key) ?? [],
              meters,
            )}
            drafts={meters}
            currency={currency}
            errors={wrongMoney.meters}
            onChange={(index, meter) =>
              form.setValue(`meters.${index}`, meter, { shouldDirty: true })
            }
            onAdd={(key) =>
              form.setValue("meters", [...meters, addedMeter(key, currency)], {
                shouldDirty: true,
              })
            }
          />
        </>
      )}
      {failure && (
        <Banner tone="destructive" title={t("plans.notSaved")}>
          {failure}
        </Banner>
      )}
      <div>
        <Button
          type="submit"
          variant="primary"
          // A plan saves only a change (6.04, 6.08); a new plan always has one to create, as the web's role editor.
          disabled={pending || (plan !== null && !form.formState.isDirty)}
          aria-busy={pending || undefined}
        >
          {plan ? t("plans.save") : t("plans.create")}
        </Button>
      </div>
      {priceAsk && plan && priceAsk.body.prices && (
        <PriceChangeDialog
          plan={plan}
          prices={priceAsk.body.prices}
          pending={pending}
          onCancel={() => setPriceAsk(null)}
          onConfirm={(change) => {
            const body = { ...priceAsk.body, priceChange: change };
            setPriceAsk(null);
            saveOn(plan, body, priceAsk.takes);
          }}
        />
      )}
      {impact && plan && (
        <ImpactDialog
          plan={plan}
          entitlements={impact.body.entitlements}
          first={impact.first}
          pending={update.isPending}
          onCancel={() => setImpact(null)}
          onConfirm={() => update.mutate({ id: plan.id, body: impact.body })}
        />
      )}
    </form>
  );
}
