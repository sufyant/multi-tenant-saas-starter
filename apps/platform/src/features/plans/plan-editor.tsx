"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  createPlan,
  ErrorCode,
  FieldErrorCode,
  getGetPlanQueryOptions,
  getListPlansQueryOptions,
  getPlanUpdatePreview,
  type ListPageOfTenantExcessResponse,
  type PlanResponse,
  ProblemDetailsError,
  problemOf,
  schemas,
  type UpdatePlanRequest,
  updatePlan,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Input } from "@repo/ui/components/input";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { buttonVariants } from "@repo/ui/components/ui/button";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect, useId, useState } from "react";
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
import { plansQuery, refreshPlans } from "@/shared/plans";
import { ProblemScreen } from "@/shared/problem-screen";
import { afterAccountRefusal } from "@/shared/refusal";
import { savedUnderKey } from "@/shared/saved-under-key";
import { SettingsNav, settingsPages } from "@/shared/settings-nav";
import { ValueField } from "@/shared/value-field";
import { ImpactDialog } from "./impact-dialog";

// The generated schema of CreatePlan and UpdatePlan (F11): the API trims the name and needs 1 to 100 characters. The values are
// one draft per key of the list, in its order, because a key's dots would nest a field's path.
const editing = z.object({
  name: z.string().trim().min(1).max(schemas.updatePlanBodyNameMax),
  isDefault: z.boolean(),
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
 * Settings › Plans › a plan (the PlanEdit, PlanEditDefault and PlanImpact boards), and New plan with `planId` null. The keys are
 * grouped by area; a feature is a switch, a limit a whole number or Unlimited, and a key with no value is closed. The default plan
 * keeps its mark until another plan takes it. No delete.
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
  if (!known || problemOf(plan.error)?.code === ErrorCode.PlanNotFound)
    return <NotFoundPage />;
  if (plan.error) return <ProblemScreen error={plan.error} />;
  if (planId !== null && !plan.data)
    return (
      <Frame>
        <Skeleton rows={4} label={t("plans.section")} />
      </Frame>
    );
  return (
    <Frame>
      <Form key={plan.data?.id ?? "new"} plan={plan.data ?? null} />
    </Frame>
  );
}

function Frame({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="plans" />
      <section className="flex min-w-0 max-w-[760px] flex-1 flex-col gap-5">
        {children}
      </section>
    </div>
  );
}

function Form({ plan }: { plan: PlanResponse | null }) {
  const t = useTranslations();
  const text = useEntitlementText();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const router = useRouter();
  const { key, renew } = useIntentKey();
  const markHint = useId();
  const plans = useQuery(getListPlansQueryOptions(plansQuery));
  const currentDefault = plans.data?.items.find((item) => item.isDefault);
  const [impact, setImpact] = useState<{
    body: UpdatePlanRequest;
    first: ListPageOfTenantExcessResponse;
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
    void refreshPlans(queryClient, saved.id);
    showToast({
      title: t.rich(created ? "plans.created" : "plans.saved", {
        name: saved.name,
        ...isolated,
      }),
    });
    router.push(settingsPages.plans);
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
      : t("plans.defaultOff", { plan: currentDefault?.name ?? "" });

  return (
    <form
      noValidate
      onSubmit={(event) => void submit(event)}
      className="flex flex-col gap-5"
    >
      <div>
        <h1 className="text-page-title">
          <bdi>{plan ? plan.name : t("plans.createTitle")}</bdi>
        </h1>
        <p className="text-body text-muted-foreground">
          {plan
            ? t("plans.organisationCount", { count: plan.tenantCount })
            : t("plans.createText")}
        </p>
      </div>
      <section className="flex flex-col gap-4 rounded-lg border bg-card p-5">
        <h2 className="text-section-title">{t("plans.section")}</h2>
        <Input
          label={t("plans.name")}
          autoComplete="off"
          className="max-w-[360px]"
          error={nameError}
          {...form.register("name")}
        />
        <div className="flex items-start gap-3">
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
          <div className="flex flex-col">
            <label
              htmlFor={`${markHint}-box`}
              className="text-body font-medium"
            >
              {t("plans.defaultPlan")}
            </label>
            <p id={markHint} className="text-small text-muted-foreground">
              {markText}
            </p>
          </div>
        </div>
      </section>
      {entitlementAreas().map(({ area, keys }) => (
        <section
          key={area}
          className="flex flex-col rounded-lg border bg-card p-5"
        >
          <h2 className="text-section-title">{text.area(area)}</h2>
          <div className="flex flex-col divide-y">
            {keys.map(({ key, isFeature }) => {
              const index = entitlementKeys.findIndex(
                (item) => item.key === key,
              );
              const draft = drafts[index];
              if (!draft) return null;
              const nameId = `${markHint}-${index}`;
              return (
                // biome-ignore lint/a11y/useSemanticElements: a fieldset's legend would draw a frame the board does not have; the group names the row.
                <div
                  key={key}
                  role="group"
                  aria-labelledby={nameId}
                  className="flex flex-col gap-3 py-4 tablet:flex-row tablet:items-start tablet:justify-between"
                >
                  <div className="flex min-w-0 flex-col gap-0.5">
                    <div className="flex flex-wrap items-baseline gap-2">
                      <span id={nameId} className="font-medium">
                        {text.name(key)}
                      </span>
                      <code
                        dir="ltr"
                        className="font-mono text-mono text-muted-foreground"
                      >
                        {key}
                      </code>
                    </div>
                    <p className="text-small text-muted-foreground">
                      {text.description(key)}
                    </p>
                  </div>
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
                </div>
              );
            })}
          </div>
        </section>
      ))}
      {failure && (
        <Banner tone="destructive" title={t("plans.notSaved")}>
          {failure}
        </Banner>
      )}
      <div className="flex flex-wrap gap-3">
        <Button type="submit" variant="primary" disabled={pending}>
          {plan ? t("plans.save") : t("plans.create")}
        </Button>
        <Link
          href={settingsPages.plans}
          className={buttonVariants({ variant: "outline" })}
        >
          {t("common.cancel")}
        </Link>
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
