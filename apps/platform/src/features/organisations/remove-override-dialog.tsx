"use client";

import {
  ErrorCode,
  problemOf,
  removeEntitlementOverride,
  type TenantDetailResponse,
  type TenantEntitlementResponse,
  type TenantPlanResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import {
  featureOf,
  retentionKey,
  useEntitlementText,
} from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";
import {
  afterRefusal,
  refreshOrganisation,
  waitsInOrganisation,
} from "./organisation";

/**
 * Remove an override (4.17): the organisation goes back to its plan's value, which the description names with the count it has
 * now. A refusal that says the organisation or the override changed meanwhile shows its text and reads again (F47).
 */
export function RemoveOverrideDialog({
  organisation,
  plan,
  row,
  onClose,
}: {
  organisation: TenantDetailResponse;
  plan: TenantPlanResponse;
  row: TenantEntitlementResponse;
  onClose: () => void;
}) {
  const t = useTranslations("planSection");
  const all = useTranslations();
  const common = useTranslations("common");
  const text = useEntitlementText();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const remove = useMutation({
    mutationFn: () => removeEntitlementOverride(organisation.id, row.key),
    onSuccess: () => {
      void refreshOrganisation(queryClient, organisation.id);
      showToast({ title: t("removed") });
      onClose();
    },
    onError: (error) => afterRefusal(queryClient, organisation.id, error),
  });

  // The Activity log goes back to the plan's period; when that is shorter than the value that applies, older entries go (4.17).
  const planDays = row.planValue?.maximum ?? null;
  const shortens =
    planDays !== null &&
    (row.effective.unlimited || (row.effective.maximum ?? 0) > planDays);
  const description = (() => {
    const value = text.label(row.isFeature, row.planValue, row.key);
    const values = {
      name: organisation.name,
      plan: plan.plan.name,
      value,
      ...isolated,
    };
    // A limit whose feature is off: the plan's value applies when the feature turns on (4.52).
    if (waitsInOrganisation(plan, row))
      return t.rich("removeDependentText", {
        ...values,
        feature: text.name(featureOf(row.key) ?? ""),
      });
    if (row.key !== retentionKey)
      return t.rich("removeText", { ...values, count: row.count });
    if (!shortens) return t.rich("removeRetentionKeeps", values);
    return t.rich("removeRetentionText", {
      ...values,
      ...text.periods,
      valueCount: planDays,
    });
  })();

  const failure = (() => {
    const problem = problemOf(remove.error);
    if (!remove.error) return null;
    if (problem?.code === ErrorCode.TenantNotActive)
      return all.rich("planSection.closedTitle", {
        name: organisation.name,
        ...isolated,
      });
    // The override was removed meanwhile: its own text, and the screen has read again.
    if (problem?.code === ErrorCode.PlanOverrideNotFound)
      return all(`errors.${problem.code}`);
    return failureText(problem);
  })();

  return (
    <Dialog
      size="wide"
      open
      onOpenChange={(open) => !open && onClose()}
      tone="destructive"
      title={t("removeTitle", { key: text.name(row.key) })}
      description={description}
      actionLabel={t("removeOverride")}
      cancelLabel={common("cancel")}
      pending={remove.isPending}
      onConfirm={() => remove.isPending || remove.mutate()}
    >
      {failure && (
        <Banner tone="destructive" title={t("notRemoved")}>
          {failure}
        </Banner>
      )}
    </Dialog>
  );
}
