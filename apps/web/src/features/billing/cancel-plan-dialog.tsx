"use client";

import {
  AuditRetention,
  changePlan,
  FeatureKey,
  getGetBillingQueryKey,
  LimitKey,
  type PlanChoiceResponse,
  problemOf,
  type SubscriptionResponse,
  type SubscriptionResponseInterval,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Dialog } from "@repo/ui/components/dialog";
import { showToast } from "@repo/ui/components/toast";
import { daysAfter } from "@repo/ui/lib/format-date-time";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { usePlan } from "@/shared/plan";
import { afterRefusal } from "@/shared/refresh";
import { useTenant } from "@/shared/tenant-context";
import { defaultPlanOf, planOf } from "./billing-facts";
import { useBillingText, useSeatFacts } from "./use-billing-text";

/**
 * Cancel the plan (10.06), or move to a lower paid plan, at the end of the paid period (B9): what the target gives less than the
 * organisation uses today, and the move, which the person can undo until then (10.07). The default plan is the cancellation.
 */
export function CancelPlanDialog({
  subscription,
  plans,
  target: given,
  interval,
  onClose,
}: {
  subscription: SubscriptionResponse;
  plans: PlanChoiceResponse[];
  /** The plan to move to; the default plan, the cancellation, when none is given. */
  target?: PlanChoiceResponse;
  interval?: SubscriptionResponseInterval;
  onClose: () => void;
}) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant, detail } = useTenant();
  const plan = usePlan();
  const seats = useSeatFacts();
  const paid = planOf(plans, subscription.planId);
  const target = given ?? defaultPlanOf(plans);
  const cancellation = target?.isDefault ?? true;
  const text = useBillingText(target?.currency ?? paid?.currency ?? "EUR");
  const date = text.date(subscription.currentPeriodEnd);
  const current = paid?.name ?? detail.planName;
  const name = target?.name ?? "";

  const move = useMutation({
    mutationFn: () =>
      changePlan(tenant.id, {
        planId: target?.id ?? "",
        interval: interval ?? subscription.interval,
        proratedAt: null,
      }),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: getGetBillingQueryKey(tenant.id),
      }),
    onError: (error) => afterRefusal(queryClient, tenant.id, error),
  });

  // What the target gives less than the organisation uses today (E4): nothing is taken away, so each line says what stays.
  const less = (() => {
    if (!target) return [];
    const lines: string[] = [];
    const members = plan.limit(LimitKey.MembersMax);
    const seatLimit = target.entitlements[LimitKey.MembersMax];
    if (typeof seatLimit === "number" && members.count > seatLimit)
      lines.push(
        t("billing.change.lessSeats", {
          plan: name,
          limit: seatLimit,
          count: members.count,
          invitations: seats?.invitations ?? 0,
        }),
      );
    const roles = plan.limit(LimitKey.RolesCustomMax);
    const rolesOn = target.entitlements[FeatureKey.RolesCustom] === true;
    const roleLimit = target.entitlements[LimitKey.RolesCustomMax];
    if (roles.count > 0 && (!rolesOn || roleLimit === 0))
      lines.push(
        t("billing.change.lessRoles", { plan: name, count: roles.count }),
      );
    else if (typeof roleLimit === "number" && roles.count > roleLimit)
      lines.push(
        t("billing.change.lessRolesLimit", {
          plan: name,
          limit: roleLimit,
          count: roles.count,
        }),
      );
    const days = target.entitlements[LimitKey.AuditRetentionDays];
    const today = detail.entitlements[LimitKey.AuditRetentionDays];
    if (
      typeof days === "number" &&
      (today === null || (typeof today === "number" && days < today))
    ) {
      // After a shorter period an older entry stays AuditRetention.Grace days before it goes (the API's K14).
      const stay = daysAfter(
        subscription.currentPeriodEnd,
        AuditRetention.Grace,
      );
      lines.push(
        t("billing.change.lessLog", {
          plan: name,
          days,
          date: text.date(stay),
        }),
      );
    }
    return lines;
  })();

  const failure = move.error
    ? {
        title: t("billing.change.failed"),
        text: failureText(problemOf(move.error)),
      }
    : null;

  return (
    <Dialog
      size="wide"
      open
      onOpenChange={(open) => !open && onClose()}
      title={
        cancellation
          ? t("billing.change.cancelTitle", { plan: current })
          : t("billing.change.moveTitle", { plan: name })
      }
      description={
        cancellation
          ? t("billing.change.cancelText", {
              date,
              default: name,
              plan: current,
              month: text.month(new Date().toISOString()),
            })
          : t("billing.change.moveText", {
              date,
              plan: name,
              current,
              month: text.month(new Date().toISOString()),
            })
      }
      tone={cancellation ? "destructive" : "default"}
      cancelLabel={t("billing.change.keep", { plan: current })}
      actionLabel={t("billing.change.moveOn", { plan: name, date })}
      pending={move.isPending}
      onConfirm={() =>
        move.mutate(undefined, {
          onSuccess: () => {
            showToast({
              title: t(
                cancellation
                  ? "billing.change.cancelScheduled"
                  : "billing.change.moveScheduled",
              ),
              description: t("billing.change.scheduledText", {
                organisation: tenant.name,
                plan: name,
                date,
              }),
            });
            onClose();
          },
        })
      }
    >
      {less.length > 0 && (
        <Banner tone="warning" title={t("billing.change.less", { plan: name })}>
          {less.map((line, index) => (
            <span key={line} className={index > 0 ? "mt-1 block" : "block"}>
              {line}
            </span>
          ))}
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
