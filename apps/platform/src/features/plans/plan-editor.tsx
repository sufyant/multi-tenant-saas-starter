"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  createPlan,
  ErrorCode,
  FieldErrorCode,
  getGetPlanQueryKey,
  getGetPlanQueryOptions,
  getListPlansQueryOptions,
  getPlanUpdatePreview,
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
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useFreshRouter } from "@repo/ui/lib/use-fresh-router";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { type ReactNode, useEffect, useId, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import {
  type Draft,
  draftOf,
  entitlementAreas,
  entitlementKeys,
  isWholeNumber,
  lowers,
  requestOf,
  useEntitlementText,
  valueFrom,
} from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";
import { NotFoundPage } from "@/shared/not-found-page";
import { planPage, plansPage, plansQuery, refreshPlans } from "@/shared/plans";
import { ProblemScreen } from "@/shared/problem-screen";
import { afterAccountRefusal } from "@/shared/refusal";
import { ValueField } from "@/shared/value-field";
import { ImpactDialog } from "./impact-dialog";

// The generated schema of UpdatePlan, which CreatePlan shares (F11): the API trims the name and needs 1 to 100 characters; the
// generated schema has the limit only, so the form adds the trim and the minimum. The values are one draft per key of the list,
// in its order, because a key's dots would nest a field's path.
const editing = schemas.UpdatePlanBody.omit({ entitlements: true }).extend({
  name: z.string().trim().min(1).max(schemas.updatePlanBodyNameMax),
  drafts: z.array(
    z.object({
      enabled: z.boolean().nullable(),
      text: z.string().refine(isWholeNumber),
      unlimited: z.boolean(),
    }),
  ),
});
type Editing = z.infer<typeof editing>;

/** The values of a plan as the form starts them: one draft per key, empty where the plan has no value. */
const draftsOf = (plan: PlanResponse | null): Draft[] =>
  entitlementKeys.map(({ key }) => draftOf(plan?.entitlements[key] ?? null));

/**
 * Exactly the values on screen (the API's UpdatePlan: a key left out is closed). Every key of the list is here, from the generated
 * client, so no key is left out by accident: a key goes out with its value, or stays out because the screen shows it unset.
 */
const bodyOf = (form: Editing): UpdatePlanRequest => ({
  name: form.name,
  isDefault: form.isDefault,
  entitlements: Object.fromEntries(
    entitlementKeys.flatMap(({ key, isFeature }, index) => {
      const draft = form.drafts[index];
      const value = draft ? valueFrom(isFeature, draft) : null;
      return value ? [[key, requestOf(value)]] : [];
    }),
  ),
});

/** Whether a save takes something away from the plan's organisations, so the impact preview runs first (the PlanImpact board). */
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
            ? t("plans.organisationCount", { count: saved.tenantCount })
            : t("plans.createText")}
        </p>
      </div>
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
                  {text.label(isFeature, plan.entitlements[key] ?? null)}
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
    },
  });
  // Another value of any field is another intent of CreatePlan: it gets a new key (F48).
  useEffect(
    () => form.subscribe({ formState: { values: true }, callback: renew }),
    [form, renew],
  );
  const drafts = form.watch("drafts");
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
    if (code === ErrorCode.PlanEntitlementInvalid) {
      const index = entitlementKeys.findIndex(
        (item) =>
          error instanceof ProblemDetailsError &&
          item.key === error.problem?.entitlement,
      );
      if (index >= 0)
        form.setError(`drafts.${index}.text`, { message: "invalid" });
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
    onSuccess: (first, { id, body }) =>
      first.total === 0
        ? update.mutate({ id, body })
        : setImpact({ body, first }),
    onError: refused,
  });
  const pending = create.isPending || update.isPending || preview.isPending;

  const submit = form.handleSubmit((values) => {
    if (pending) return;
    const body = bodyOf(values);
    if (!plan) create.mutate({ body, key });
    else if (takesAway(plan, values)) preview.mutate({ id: plan.id, body });
    else update.mutate({ id: plan.id, body });
  });

  const failure = (() => {
    const error = create.error ?? update.error ?? preview.error;
    const problem = problemOf(error);
    const code = problem?.code;
    if (
      !error ||
      code === ErrorCode.PlanNameTaken ||
      code === ErrorCode.PlanNameInvalid ||
      code === ErrorCode.PlanEntitlementInvalid ||
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
  const setDraft = (index: number, draft: Draft) => {
    form.setValue(`drafts.${index}`, draft, { shouldDirty: true });
    form.clearErrors(`drafts.${index}.text`);
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
            return (
              <Row
                key={key}
                group
                labelId={`${markHint}-${index}`}
                label={text.name(key)}
                description={text.description(key)}
              >
                <ValueField
                  isFeature={isFeature}
                  label={text.name(key)}
                  draft={draft}
                  onChange={(next) => setDraft(index, next)}
                  error={
                    errors.drafts?.[index]?.text
                      ? t("plans.numberInvalid")
                      : undefined
                  }
                  showUnset
                />
              </Row>
            );
          })}
        </Area>
      ))}
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
