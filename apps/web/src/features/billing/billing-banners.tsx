"use client";

import {
  getGetBillingQueryKey,
  PaymentResponseState,
  type PlanChoiceResponse,
  problemOf,
  type SubscriptionResponse,
  undoScheduledChange,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { refreshTenant } from "@/shared/refresh";
import { useTenant } from "@/shared/tenant-context";
import { usePortal } from "@/shared/use-portal";
import { defaultPlanOf, feesOf, planOf, totalOf } from "./billing-facts";
import { useBillingText, useSeatFacts } from "./use-billing-text";

/**
 * The Billing page's banners, above the plan (10.07, 10.15 to 10.18): a failed payment, while Stripe retries, in billing's grace, or
 * after the move to the default plan, each with the portal that pays; a cancellation or a move scheduled at the period's end, with
 * Undo; and a price change the organisation was told of, which the viewer can close.
 */
export function BillingBanners({
  subscription,
  plans,
  paid,
}: {
  subscription: SubscriptionResponse;
  plans: PlanChoiceResponse[];
  paid: PlanChoiceResponse | undefined;
}) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { tenant } = useTenant();
  const portal = usePortal();
  const seats = useSeatFacts();
  const currency = (paid ?? plans[0])?.currency ?? "EUR";
  const text = useBillingText(currency);
  const fallback = defaultPlanOf(plans);
  const planName = paid?.name ?? "";
  const undo = useMutation({
    mutationFn: () => undoScheduledChange(tenant.id),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({
          queryKey: getGetBillingQueryKey(tenant.id),
        }),
        refreshTenant(queryClient, tenant.id),
      ]),
  });
  const closedKey = `billing.priceChange.closed.${tenant.id}`;
  const [closed, setClosed] = useState(() => {
    try {
      return window.localStorage.getItem(closedKey);
    } catch {
      return null;
    }
  });

  const banners = [];
  const payment = subscription.payment;
  if (payment?.state === PaymentResponseState.Retrying)
    banners.push(
      <Banner
        key="payment-retry"
        tone="warning"
        title={t("billing.retrying", {
          date: text.date(payment.nextAttemptAt ?? payment.failedAt),
        })}
        action={
          <Button disabled={portal.pending} onClick={portal.open}>
            {t("billing.updateCard")}
          </Button>
        }
      >
        {t("billing.retryingText", {
          card: text.card(payment.card),
          date: text.date(payment.failedAt),
        })}
      </Banner>,
    );
  if (payment?.state === PaymentResponseState.Grace)
    banners.push(
      <Banner
        key="payment-grace"
        tone="destructive"
        title={t("billing.grace", {
          default: fallback?.name ?? "",
          date: text.date(payment.graceEndsAt ?? payment.failedAt),
        })}
        action={
          <Button disabled={portal.pending} onClick={portal.open}>
            {t("billing.payNow")}
          </Button>
        }
      >
        {t("billing.graceText", {
          card: text.card(payment.card),
          count: payment.attempts,
          date: text.date(payment.failedAt),
          end: text.date(payment.graceEndsAt ?? payment.failedAt),
        })}
      </Banner>,
    );
  if (payment?.state === PaymentResponseState.MovedToDefault)
    banners.push(
      <Banner
        key="moved"
        tone="destructive"
        title={t("billing.moved", {
          organisation: tenant.name,
          default: fallback?.name ?? "",
          date: text.date(payment.movedAt ?? payment.failedAt),
        })}
        action={
          <Button disabled={portal.pending} onClick={portal.open}>
            {t("billing.payNow")}
          </Button>
        }
      >
        {t("billing.movedText", { plan: planName })}
      </Banner>,
    );

  const scheduled = subscription.scheduledChange;
  if (!payment && (subscription.cancelAtPeriodEnd || scheduled)) {
    const target = subscription.cancelAtPeriodEnd
      ? fallback
      : planOf(plans, scheduled?.planId);
    banners.push(
      <Banner
        key="move-at-end"
        tone="info"
        title={t("billing.cancelled", {
          date: text.date(subscription.currentPeriodEnd),
          plan: target?.name ?? "",
        })}
        action={
          <Button
            disabled={undo.isPending}
            onClick={() =>
              undo.mutate(undefined, {
                onSuccess: () =>
                  showToast({
                    title: t("billing.undone"),
                    description: t("billing.undoneText", {
                      organisation: tenant.name,
                      plan: planName,
                    }),
                  }),
                onError: (error) =>
                  showToast({
                    tone: "destructive",
                    title: t("billing.undoFailed"),
                    description: failureText(problemOf(error)),
                  }),
              })
            }
          >
            {t("billing.undo")}
          </Button>
        }
      >
        {t("billing.cancelledText", { plan: planName })}
      </Banner>,
    );
  }

  const change = subscription.priceChange;
  if (change && closed !== change.movesAt) {
    const oldFees = feesOf(change.oldPrices, subscription.interval);
    const newFees = feesOf(change.newPrices, subscription.interval);
    const people = seats?.people ?? 0;
    // The fee per person's sentence; a change of the base fee alone has its own (13.21's copy note).
    const base = oldFees.perPerson === newFees.perPerson;
    banners.push(
      <Banner
        key="price"
        tone="info"
        closeLabel={t("common.close")}
        onClose={() => {
          setClosed(change.movesAt);
          try {
            window.localStorage.setItem(closedKey, change.movesAt);
          } catch {
            // A private window keeps nothing; the banner shows again next time.
          }
        }}
        title={t(base ? "billing.priceChangeBase" : "billing.priceChange", {
          date: text.date(change.movesAt),
          plan: planName,
          old: text.money(base ? oldFees.base : oldFees.perPerson),
          new: text.money(base ? newFees.base : newFees.perPerson),
          interval: subscription.interval,
        })}
      >
        {seats
          ? t("billing.priceChangeText", {
              people,
              newTotal: text.money(totalOf(newFees, people)),
              oldTotal: text.money(totalOf(oldFees, people)),
              date: text.date(change.movesAt),
              interval: subscription.interval,
            })
          : null}
      </Banner>,
    );
  }
  return banners.length > 0 ? (
    <div className="flex flex-col gap-2">{banners}</div>
  ) : null;
}
