"use client";

import {
  changeTenantPlan,
  ErrorCode,
  getListPlansQueryOptions,
  getTenantPlanChangePreview,
  problemOf,
  type TenantDetailResponse,
  type TenantPlanResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Combobox } from "@repo/ui/components/combobox";
import { Dialog } from "@repo/ui/components/dialog";
import { Status } from "@repo/ui/components/status";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { isFeatureKey, useEntitlementText } from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";
import { plansQuery, refreshPlans } from "@/shared/plans";
import { afterRefusal, refreshOrganisation } from "./organisation";

/**
 * Change an organisation's plan (the OrgPlan board): the plans, the default and the current one marked; for the plan chosen, the
 * preview of this organisation lists what goes over or turns off, and each override that still applies. It never blocks.
 */
export function ChangePlanDialog({
  organisation,
  plan,
  onClose,
}: {
  organisation: TenantDetailResponse;
  plan: TenantPlanResponse;
  onClose: () => void;
}) {
  const t = useTranslations("planSection");
  const all = useTranslations();
  const common = useTranslations("common");
  const text = useEntitlementText();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const plans = useQuery(getListPlansQueryOptions(plansQuery));
  const [chosen, setChosen] = useState(plan.plan.id);
  const chosenName =
    plans.data?.items.find((item) => item.id === chosen)?.name ?? "";
  const preview = useQuery({
    queryKey: ["tenant-plan-change-preview", organisation.id, chosen],
    queryFn: () =>
      getTenantPlanChangePreview(organisation.id, { planId: chosen }),
    enabled: chosen !== plan.plan.id,
  });
  const change = useMutation({
    mutationFn: () => changeTenantPlan(organisation.id, { planId: chosen }),
    onSuccess: () => {
      void refreshOrganisation(queryClient, organisation.id);
      showToast({
        title: t.rich("changed", {
          name: organisation.name,
          plan: chosenName,
          ...isolated,
        }),
      });
      onClose();
    },
    onError: (error) => {
      afterRefusal(queryClient, organisation.id, error);
      if (problemOf(error)?.code === ErrorCode.PlanNotFound)
        void refreshPlans(queryClient);
    },
  });

  const rowOf = (key: string) =>
    plan.entitlements.find((row) => row.key === key);
  const excesses = preview.data?.excesses ?? [];

  const failure = (() => {
    const problem = problemOf(change.error);
    if (!change.error) return null;
    if (problem?.code === ErrorCode.TenantNotActive)
      return all.rich("planSection.closedTitle", {
        name: organisation.name,
        ...isolated,
      });
    if (problem?.code === ErrorCode.PlanNotFound)
      return all(`errors.${problem.code}`);
    return failureText(problem);
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t.rich("changeTitle", { name: organisation.name, ...isolated })}
      description={
        excesses.length > 0
          ? t.rich("changeText", {
              name: organisation.name,
              plan: chosenName,
              ...isolated,
            })
          : undefined
      }
      actionLabel={t("changePlan")}
      cancelLabel={common("cancel")}
      actionDisabled={chosen === plan.plan.id}
      pending={change.isPending}
      onConfirm={() => change.isPending || change.mutate()}
    >
      <div className="flex flex-col gap-3.5">
        <Combobox
          label={t("newPlan")}
          value={chosen}
          options={(plans.data?.items ?? []).map((item) => ({
            id: item.id,
            label: item.name,
            hint:
              item.id === plan.plan.id
                ? t("current")
                : item.isDefault
                  ? all("plans.default")
                  : undefined,
          }))}
          onValueChange={setChosen}
          searchPlaceholder={t("planSearch")}
          emptyLabel={t("planEmpty")}
        />
        {preview.data && (
          <ul className="flex flex-col gap-2 text-body">
            {excesses.map((excess) => (
              <li
                key={excess.key}
                className="flex flex-wrap items-center gap-2"
              >
                <span>
                  {isFeatureKey(excess.key)
                    ? text.turnsOff(excess.key, excess.count)
                    : t("limitOver", {
                        key: text.name(excess.key),
                        count: excess.count,
                        limit: text.label(false, excess.newValue),
                        plan: chosenName,
                      })}
                </span>
                {!isFeatureKey(excess.key) && (
                  <Status tone="warning">
                    {t("overBy", { count: excess.amount })}
                  </Status>
                )}
              </li>
            ))}
            {preview.data.overriddenKeys.map((key) => {
              const row = rowOf(key);
              return (
                <li key={key} className="text-muted-foreground">
                  {t("overrideApplies", {
                    key: text.name(key),
                    value: text.label(
                      row?.isFeature ?? false,
                      row?.override?.value ?? null,
                    ),
                  })}
                </li>
              );
            })}
          </ul>
        )}
        {preview.isError && (
          <Banner tone="destructive" title={all("plans.previewError")}>
            {failureText(problemOf(preview.error))}
          </Banner>
        )}
        {failure && (
          <Banner tone="destructive" title={t("notChanged")}>
            {failure}
          </Banner>
        )}
      </div>
    </Dialog>
  );
}
