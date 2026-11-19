"use client";

import {
  changePlan,
  ErrorCode,
  FeatureKey,
  getGetBillingQueryKey,
  LimitKey,
  type PlanChoiceResponse,
  previewPlanChange,
  problemOf,
  type SubscriptionResponse,
  type SubscriptionResponseInterval,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Dialog } from "@repo/ui/components/dialog";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useFreshRouter } from "@repo/ui/lib/use-fresh-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { refreshTenant } from "@/shared/refresh";
import { useTenant } from "@/shared/tenant-context";
import { feesOf, planOf } from "./billing-facts";
import { useBillingText, useSeatFacts } from "./use-billing-text";
import { usePortal } from "./use-portal";

/**
 * An upgrade, or monthly to yearly, at once (10.04, 10.05; B9): Stripe's preview of what the rest of the period costs today and what
 * the new plan costs from the next period, then the move at the preview's moment, so it charges the amount shown. A declined card
 * says so in the dialog and offers the portal (10.13). The thread's choice: the card it pays with is not named, as GetBilling
 * answers no card.
 */
export function UpgradeDialog({
  subscription,
  plans,
  target,
  interval,
  onClose,
}: {
  subscription: SubscriptionResponse;
  plans: PlanChoiceResponse[];
  target: PlanChoiceResponse;
  interval: SubscriptionResponseInterval;
  onClose: () => void;
}) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const router = useFreshRouter(useRouter());
  const { tenant } = useTenant();
  const seats = useSeatFacts();
  const portal = usePortal();
  const text = useBillingText(target.currency);
  const paid = planOf(plans, subscription.planId);
  const samePlan = target.id === subscription.planId;
  const preview = useQuery({
    queryKey: ["billing-preview", tenant.id, target.id, interval],
    queryFn: () =>
      previewPlanChange(tenant.id, { planId: target.id, interval }),
    staleTime: 0,
    gcTime: 0,
  });
  const move = useMutation({
    mutationFn: () =>
      changePlan(tenant.id, {
        planId: target.id,
        interval,
        proratedAt: preview.data?.proratedAt ?? null,
      }),
  });

  const limits = (() => {
    const list: string[] = [];
    const seatLimit = target.entitlements[LimitKey.MembersMax];
    list.push(
      typeof seatLimit === "number"
        ? t("billing.plans.seats", { count: seatLimit })
        : t("billing.plans.seatsUnlimited"),
    );
    const roles = target.entitlements[LimitKey.RolesCustomMax];
    const rolesOn = target.entitlements[FeatureKey.RolesCustom] === true;
    list.push(
      !rolesOn || roles === 0
        ? t("billing.plans.noCustomRoles")
        : typeof roles === "number"
          ? t("billing.plans.customRoles", { count: roles })
          : t("billing.plans.customRolesUnlimited"),
    );
    return new Intl.ListFormat(t("locale.format"), {
      type: "conjunction",
    }).format(list);
  })();

  const declined =
    problemOf(move.error)?.code === ErrorCode.BillingPaymentDeclined;
  const failure = (() => {
    const error = move.error ?? preview.error;
    if (!error || declined) return null;
    return {
      title: t(
        move.error ? "billing.change.failed" : "billing.change.previewFailed",
      ),
      text: failureText(problemOf(error)),
    };
  })();
  const today = preview.data?.amountToday ?? null;

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("billing.change.title", {
        old: paid?.name ?? "",
        new: samePlan ? t("billing.plans.yearly") : target.name,
      })}
      description={
        samePlan
          ? t("billing.change.yearlyText")
          : t("billing.change.upgradeText", { plan: target.name, limits })
      }
      cancelLabel={t("common.cancel")}
      actionLabel={t(
        samePlan
          ? "billing.change.switchAndPay"
          : "billing.change.upgradeAndPay",
        { amount: text.money(today ?? 0) },
      )}
      actionDisabled={!preview.data || today === null}
      actionReasonId="upgrade-preview"
      pending={move.isPending}
      onConfirm={() =>
        move.mutate(undefined, {
          onSuccess: async () => {
            await Promise.all([
              queryClient.invalidateQueries({
                queryKey: getGetBillingQueryKey(tenant.id),
              }),
              refreshTenant(queryClient, tenant.id),
            ]);
            showToast({
              title: t("billing.change.upgraded", { plan: target.name }),
              description: t("billing.change.charged", {
                amount: text.money(today ?? 0),
              }),
            });
            router.push(`/${tenant.slug}/settings/billing`);
          },
        })
      }
    >
      <div id="upgrade-preview" className="contents">
        {preview.isPending ? (
          <Skeleton rows={2} label={t("billing.loading")} />
        ) : preview.data ? (
          <dl className="flex flex-col gap-2 rounded-md bg-muted p-3 text-small">
            <div className="flex items-start justify-between gap-4">
              <dt className="text-muted-foreground">
                {t("billing.change.today", {
                  month: text.month(preview.data.proratedAt ?? preview.data.at),
                })}
              </dt>
              <dd className="font-medium">
                {t("billing.change.excl", { amount: text.money(today ?? 0) })}
              </dd>
            </div>
            <div className="flex items-start justify-between gap-4">
              <dt className="text-muted-foreground">
                {t("billing.change.from", {
                  date: text.date(subscription.currentPeriodEnd),
                })}
              </dt>
              <dd className="text-end">
                {seats
                  ? text.price(
                      feesOf(target.prices, interval),
                      seats.people,
                      interval,
                    )
                  : text.money(preview.data.amount)}
              </dd>
            </div>
          </dl>
        ) : null}
      </div>
      {declined && (
        <Banner
          tone="destructive"
          title={t("billing.change.declined")}
          action={
            <Button disabled={portal.pending} onClick={portal.open}>
              {t("billing.change.anotherCard")}
            </Button>
          }
        >
          {t("billing.change.declinedText")}
        </Banner>
      )}
      {failure && (
        <Banner tone="destructive" title={failure.title}>
          {failure.text}
        </Banner>
      )}
    </Dialog>
  );
}
