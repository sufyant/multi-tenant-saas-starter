"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  ErrorCode,
  FieldErrorCode,
  ProblemDetailsError,
  problemOf,
  schemas,
  setEntitlementOverride,
  type TenantDetailResponse,
  type TenantPlanResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Combobox } from "@repo/ui/components/combobox";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { calendarDay, startOfDay } from "@repo/ui/lib/format-date-time";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import {
  type Draft,
  draftOf,
  isWholeNumber,
  requestOf,
  useEntitlementText,
  valueFrom,
} from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";
import { useDates } from "@/shared/use-dates";
import { ValueField } from "@/shared/value-field";
import { afterRefusal, refreshOrganisation } from "./organisation";

// The generated schema of SetEntitlementOverride (F11): the reason is trimmed and 1 to 500 characters, as the API counts it. The
// expiry is a calendar day of the date field, or empty for none.
const overriding = z.object({
  key: z.string(),
  draft: z.object({
    enabled: z.boolean().nullable(),
    text: z.string().refine(isWholeNumber),
    unlimited: z.boolean(),
  }),
  expires: z.string(),
  reason: z
    .string()
    .trim()
    .min(1)
    .max(schemas.setEntitlementOverrideBodyReasonMax),
});
type Overriding = z.infer<typeof overriding>;

/**
 * Add or edit an override of one key (the OrgPlan board): the value, an optional expiry and a required reason. An edit starts from
 * the override; an addition from the value that applies. The expiry is the first moment of the chosen day in the system admin's
 * zone, as the field's hint says. A key comes from the row, or, from Add override, from the list.
 */
export function OverrideDialog({
  organisation,
  plan,
  entitlementKey,
  onClose,
}: {
  organisation: TenantDetailResponse;
  plan: TenantPlanResponse;
  /** The row's key, or null to choose one. */
  entitlementKey: string | null;
  onClose: () => void;
}) {
  const t = useTranslations("planSection");
  const all = useTranslations();
  const common = useTranslations("common");
  const text = useEntitlementText();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { timeZone } = useDates();
  const rowOf = (key: string) =>
    plan.entitlements.find((row) => row.key === key) ?? plan.entitlements[0];
  const first = rowOf(entitlementKey ?? plan.entitlements[0]?.key ?? "");
  const editing = entitlementKey !== null && first?.override != null;
  const startOf = (key: string): Draft => {
    const row = rowOf(key);
    return draftOf(row?.override?.value ?? row?.effective ?? null);
  };
  const form = useForm<Overriding>({
    resolver: zodResolver(overriding),
    defaultValues: {
      key: first?.key ?? "",
      draft: startOf(first?.key ?? ""),
      expires:
        editing && first?.override?.expiresAt
          ? calendarDay(first.override.expiresAt, { timeZone })
          : "",
      reason: editing ? (first?.override?.reason ?? "") : "",
    },
  });
  const key = form.watch("key");
  const draft = form.watch("draft");
  const isFeature = rowOf(key)?.isFeature ?? false;

  const save = useMutation({
    mutationFn: (values: Overriding) => {
      const value = valueFrom(isFeature, values.draft);
      return setEntitlementOverride(organisation.id, values.key, {
        value: requestOf(
          value ?? { enabled: false, maximum: null, unlimited: false },
        ),
        expiresAt: values.expires
          ? startOfDay(values.expires, { timeZone })
          : null,
        reason: values.reason,
      });
    },
    onSuccess: () => {
      void refreshOrganisation(queryClient, organisation.id);
      showToast({ title: t("saved") });
      onClose();
    },
    onError: (error) => {
      afterRefusal(queryClient, organisation.id, error);
      if (!(error instanceof ProblemDetailsError)) return;
      if (error.problem?.code === ErrorCode.PlanEntitlementInvalid)
        form.setError("draft.text", { message: "invalid" });
      const fields = error.problem?.errors ?? {};
      const [reason] = fields.reason ?? [];
      if (reason) form.setError("reason", { message: reason });
      const [expires] = fields.expiresAt ?? [];
      if (expires) form.setError("expires", { message: expires });
    },
  });

  const submit = form.handleSubmit((values) => {
    // A limit needs a number or Unlimited: an override always has a value.
    if (valueFrom(isFeature, values.draft) === null) {
      form.setError("draft.text", { message: "invalid" });
      return;
    }
    if (!save.isPending) save.mutate(values);
  });

  const failure = (() => {
    const problem = problemOf(save.error);
    const code = problem?.code;
    if (
      !save.error ||
      code === ErrorCode.RequestValidationFailed ||
      code === ErrorCode.PlanEntitlementInvalid
    )
      return null;
    // The organisation was closed meanwhile: the board's sentence, and the screen has read again (F47).
    if (code === ErrorCode.TenantNotActive)
      return all.rich("planSection.closedTitle", {
        name: organisation.name,
        ...isolated,
      });
    return failureText(problem);
  })();

  const errors = form.formState.errors;
  const reasonError = errors.reason?.message
    ? errors.reason.message === FieldErrorCode.TooLong ||
      errors.reason.message === "validation.too_long"
      ? t("reasonTooLong", { max: schemas.setEntitlementOverrideBodyReasonMax })
      : t("reasonRequired")
    : undefined;

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={
        editing
          ? t("editTitle", { key: text.name(key) })
          : t.rich("addTitle", { name: organisation.name, ...isolated })
      }
      description={t.rich("overrideText", {
        name: organisation.name,
        ...isolated,
      })}
      actionLabel={t("save")}
      cancelLabel={common("cancel")}
      pending={save.isPending}
      onConfirm={() => void submit()}
    >
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-3.5"
      >
        {editing ? (
          <div className="flex flex-col gap-1.5">
            <span className="text-body font-medium">{t("key")}</span>
            <span className="flex flex-wrap items-baseline gap-2">
              <span className="font-medium">{text.name(key)}</span>
              <code
                dir="ltr"
                className="font-mono text-mono text-muted-foreground"
              >
                {key}
              </code>
            </span>
          </div>
        ) : (
          <Combobox
            label={t("key")}
            value={key}
            options={plan.entitlements.map((row) => ({
              id: row.key,
              label: text.name(row.key),
              keywords: [row.key],
            }))}
            onValueChange={(next) => {
              form.setValue("key", next);
              form.setValue("draft", startOf(next));
              form.clearErrors("draft");
            }}
            searchPlaceholder={t("keySearch")}
            emptyLabel={t("keyEmpty")}
          />
        )}
        <div className="flex flex-col gap-1.5">
          <span className="text-body font-medium">{t("value")}</span>
          <div className="flex justify-start">
            <ValueField
              isFeature={isFeature}
              label={t("value")}
              draft={draft}
              onChange={(next) => {
                form.setValue("draft", next);
                form.clearErrors("draft");
              }}
              error={
                errors.draft?.text ? all("plans.numberInvalid") : undefined
              }
            />
          </div>
        </div>
        <Input
          type="date"
          label={t("expires")}
          hint={t("expiresHint")}
          error={errors.expires ? t("expiryInvalid") : undefined}
          {...form.register("expires")}
        />
        <Input
          label={t("reason")}
          hint={t("reasonHint")}
          autoComplete="off"
          maxLength={schemas.setEntitlementOverrideBodyReasonMax}
          error={reasonError}
          {...form.register("reason")}
        />
        {failure && (
          <Banner tone="destructive" title={t("notSaved")}>
            {failure}
          </Banner>
        )}
      </form>
    </Dialog>
  );
}
