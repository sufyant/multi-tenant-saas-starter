"use client";

import {
  ErrorCode,
  problemOf,
  removeEntitlementOverride,
  type TenantDetailResponse,
  type TenantEntitlementResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useEntitlementText } from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";
import { afterRefusal, refreshOrganisation } from "./organisation";

/**
 * Remove an override (the OrgPlan board): the organisation goes back to the plan's value, which the description names with the
 * count it has now. A refusal that says the organisation or the override changed meanwhile shows its text and reads again (F47).
 */
export function RemoveOverrideDialog({
  organisation,
  row,
  onClose,
}: {
  organisation: TenantDetailResponse;
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
      open
      onOpenChange={(open) => !open && onClose()}
      tone="destructive"
      title={t("removeTitle", { key: text.name(row.key) })}
      description={t.rich("removeText", {
        name: organisation.name,
        value: text.label(row.isFeature, row.planValue),
        count: row.count,
        ...isolated,
      })}
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
