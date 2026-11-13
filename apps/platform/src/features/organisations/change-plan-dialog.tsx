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
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import {
  entitlementAreas,
  isFeatureKey,
  retentionKey,
  useEntitlementText,
} from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";
import { plansQuery, refreshPlans } from "@/shared/plans";
import { useDates } from "@/shared/use-dates";
import { afterRefusal, refreshOrganisation } from "./organisation";

/**
 * Change an organisation's plan (4.15): the plans, the default and the current one marked; for the plan chosen, the preview of this
 * organisation says from which plan to which, then what goes over or turns off, and each override that stays. It never blocks.
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
  const { date } = useDates();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const plans = useQuery(getListPlansQueryOptions(plansQuery));
  const [chosen, setChosen] = useState(plan.plan.id);
  const chosenName =
    plans.data?.items.find((item) => item.id === chosen)?.name ?? "";
  const preview = useQuery({
    // A POST that reads: its key is its operation and its arguments, as a generated key is (F2, F44).
    queryKey: [
      "getTenantPlanChangePreview",
      organisation.id,
      { planId: chosen },
    ],
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
  // The keys in the frames' order of areas (4.15): the seats, then the roles.
  const order = entitlementAreas().flatMap(({ keys }) =>
    keys.map(({ key }) => key),
  );
  const excesses = [...(preview.data?.excesses ?? [])].sort(
    (one, other) => order.indexOf(one.key) - order.indexOf(other.key),
  );

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
      size="wide"
      open
      onOpenChange={(open) => !open && onClose()}
      title={t.rich("changeTitle", { name: organisation.name, ...isolated })}
      description={
        chosen !== plan.plan.id &&
        preview.data &&
        t.rich(
          excesses.length > 0 ||
            preview.data.overriddenKeys.length > 0 ||
            preview.data.retentionShortened
            ? "changeFromToWhat"
            : "changeFromTo",
          { from: plan.plan.name, to: chosenName, ...isolated },
        )
      }
      actionLabel={
        chosen !== plan.plan.id
          ? t("changeTo", { plan: chosenName })
          : t("changePlan")
      }
      cancelLabel={common("cancel")}
      actionDisabled={chosen === plan.plan.id}
      pending={change.isPending}
      onConfirm={() => change.isPending || change.mutate()}
    >
      <div className="flex flex-col gap-3.5">
        {preview.data && (
          <ul className="flex flex-col gap-2 text-small text-muted-foreground">
            {excesses.map((excess) => (
              <li key={excess.key}>
                {isFeatureKey(excess.key)
                  ? text.turnsOff(excess.key, excess.count, chosenName)
                  : t.rich("limitChange", {
                      ...isolated,
                      key: text.name(excess.key),
                      from: text.label(
                        false,
                        rowOf(excess.key)?.effective ?? null,
                      ),
                      to: text.label(false, excess.newValue),
                      count: excess.count,
                      name: organisation.name,
                    })}
              </li>
            ))}
            {preview.data.retentionShortened && (
              <li>
                {t("retentionChange", {
                  ...text.periods,
                  key: text.name(retentionKey),
                  from:
                    preview.data.retentionShortened.fromDays === null
                      ? "none"
                      : String(preview.data.retentionShortened.fromDays),
                  to: text.days(preview.data.retentionShortened.toDays),
                  toCount: preview.data.retentionShortened.toDays,
                })}
              </li>
            )}
            {preview.data.overriddenKeys.map((key) => {
              const expiresAt = rowOf(key)?.override?.expiresAt;
              return (
                <li key={key}>
                  {expiresAt
                    ? t("overrideStaysUntil", {
                        key: text.name(key),
                        date: date(expiresAt),
                      })
                    : t("overrideStays", { key: text.name(key) })}
                </li>
              );
            })}
          </ul>
        )}
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
