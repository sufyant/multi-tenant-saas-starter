"use client";

import {
  BillingResponseBilledBy,
  ErrorCode,
  getGetTenantBillingQueryKey,
  getGetTenantBillingQueryOptions,
  getListPlansQueryOptions,
  PaymentResponseState,
  problemOf,
  SetBilledByRequestBilledBy,
  type SubscriptionResponse,
  SubscriptionResponseStatus,
  setBilledBy,
  type TenantDetailResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Dialog } from "@repo/ui/components/dialog";
import { Label } from "@repo/ui/components/label";
import { Pill } from "@repo/ui/components/pill";
import { showToast } from "@repo/ui/components/toast";
import { formatMoney } from "@repo/ui/lib/format-money";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { plansQuery } from "@/shared/plans";
import { useDates } from "@/shared/use-dates";
import { Row } from "./card-row";

const live = (subscription: SubscriptionResponse | null) =>
  subscription !== null &&
  subscription.status !== SubscriptionResponseStatus.Canceled &&
  subscription.status !== SubscriptionResponseStatus.IncompleteExpired;

/**
 * The Plan and billing tab's Billing card (4.27, 4.31 to 4.36; B11, B12): how the organisation pays, with Switch to Contract and
 * Switch to Card; the period it pays for; where a failed payment stands; and a scheduled cancellation or move. From
 * GetTenantBilling, which answers 404 `billing.off` while the deployment's billing is off: then the card does not show. A closed or
 * purging organisation's billing is read-only.
 */
export function BillingCard({
  organisation,
}: {
  organisation: TenantDetailResponse;
}) {
  const t = useTranslations("billingCard");
  const common = useTranslations("common");
  const locale = useLocale();
  const dates = useDates();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const billing = useQuery({
    ...getGetTenantBillingQueryOptions(organisation.id),
    retry: false,
  });
  const plans = useQuery(getListPlansQueryOptions(plansQuery));
  const [asking, setAsking] = useState<SetBilledByRequestBilledBy | null>(null);
  const switchTo = useMutation({
    mutationFn: (billedBy: SetBilledByRequestBilledBy) =>
      setBilledBy(organisation.id, { billedBy }),
    onSuccess: (answer) =>
      queryClient.setQueryData(
        getGetTenantBillingQueryKey(organisation.id),
        answer,
      ),
  });
  if (problemOf(billing.error)?.code === ErrorCode.BillingOff) return null;
  if (!billing.data) return null;

  const data = billing.data;
  const subscription = live(data.subscription) ? data.subscription : null;
  const items = plans.data?.items ?? [];
  const nameOf = (id: string | undefined) =>
    items.find((plan) => plan.id === id)?.name ?? "";
  const fallback = items.find((plan) => plan.isDefault)?.name ?? "";
  const date = dates.date;
  const contract = data.billedBy === BillingResponseBilledBy.Contract;
  const editable =
    organisation.status !== TenantResponseStatus.Closed &&
    organisation.status !== TenantResponseStatus.Purging;
  const payment = subscription?.payment ?? null;

  const paymentRow: ReactNode = (() => {
    if (contract || !subscription) return null;
    const money = (minor: number) =>
      formatMoney(minor, payment?.currency ?? "EUR", locale);
    if (!payment)
      return (
        <span>
          <Pill tone="success">{t("paid")}</Pill>
        </span>
      );
    if (payment.state === PaymentResponseState.Retrying)
      return (
        <>
          <span>
            <Pill tone="warning">
              {t("retryingSince", { date: date(payment.failedAt) })}
            </Pill>
          </span>
          <span className="text-small text-muted-foreground">
            {t("retryingText", {
              amount: money(payment.amountDue),
              date: date(payment.nextAttemptAt ?? payment.failedAt),
            })}
          </span>
        </>
      );
    if (payment.state === PaymentResponseState.Grace)
      return (
        <>
          <span>
            <Pill tone="destructive">
              {t("graceUntil", {
                date: date(payment.graceEndsAt ?? payment.failedAt),
              })}
            </Pill>
          </span>
          <span className="text-small text-muted-foreground">
            {t("graceText", {
              count: payment.attempts,
              date: date(payment.graceEndsAt ?? payment.failedAt),
              default: fallback,
            })}
          </span>
        </>
      );
    return (
      <>
        <span>
          <Pill tone="destructive">
            {t("moved", {
              default: fallback,
              date: date(payment.movedAt ?? payment.failedAt),
            })}
          </Pill>
        </span>
        <span className="text-small text-muted-foreground">
          {t("movedText", { plan: nameOf(subscription.planId) })}
        </span>
      </>
    );
  })();

  const scheduled: ReactNode = (() => {
    if (!subscription) return "—";
    if (subscription.cancelAtPeriodEnd)
      return (
        <>
          <span>
            {t("ends", {
              plan: nameOf(subscription.planId),
              date: date(subscription.currentPeriodEnd),
            })}
          </span>
          <span className="text-small text-muted-foreground">
            {t("movesTo", {
              plan: fallback,
              date: date(subscription.currentPeriodEnd),
            })}
          </span>
        </>
      );
    if (subscription.scheduledChange)
      return t("movesTo", {
        plan: nameOf(subscription.scheduledChange.planId),
        date: date(subscription.currentPeriodEnd),
      });
    return "—";
  })();

  const target =
    asking === SetBilledByRequestBilledBy.Contract
      ? {
          title: t("contractTitle", { organisation: organisation.name }),
          text: subscription
            ? t("contractDialogText", {
                date: date(subscription.currentPeriodEnd),
              })
            : t("contractDialogTextNoCard"),
          note: t("contractDialogNote"),
          action: t("markContract"),
        }
      : {
          title: t("cardTitle", { organisation: organisation.name }),
          text: t("cardDialogText"),
          note: null,
          action: t("toCard"),
        };

  return (
    <section
      aria-label={t("title")}
      className="flex flex-col divide-y divide-border rounded-lg border border-border bg-card"
    >
      <h2 className="px-5 py-4 text-section-title">{t("title")}</h2>
      <Row
        label={t("billedBy")}
        actions={
          editable && (
            <Button
              onClick={() =>
                setAsking(
                  contract
                    ? SetBilledByRequestBilledBy.Card
                    : SetBilledByRequestBilledBy.Contract,
                )
              }
            >
              {contract ? t("toCard") : t("toContract")}
            </Button>
          )
        }
      >
        <span>
          <Label>{contract ? t("contractLabel") : t("cardLabel")}</Label>
        </span>
        <span className="text-small text-muted-foreground">
          {contract ? t("contractText") : t("cardText")}
        </span>
      </Row>
      {!contract && (
        <Row label={t("period")}>
          {subscription ? (
            <span>{t(`interval.${subscription.interval}`)}</span>
          ) : (
            <span className="text-muted-foreground">{t("noSubscription")}</span>
          )}
        </Row>
      )}
      {paymentRow && <Row label={t("payment")}>{paymentRow}</Row>}
      {!contract && <Row label={t("scheduledChange")}>{scheduled}</Row>}
      {asking && (
        <Dialog
          open
          onOpenChange={(open) => !open && setAsking(null)}
          title={target.title}
          description={target.text}
          cancelLabel={common("cancel")}
          actionLabel={target.action}
          pending={switchTo.isPending}
          onConfirm={() =>
            switchTo.mutate(asking, {
              onSuccess: () => {
                setAsking(null);
                showToast({ title: t("switched") });
              },
              onError: (error) =>
                showToast({
                  tone: "destructive",
                  title: t("switchFailed"),
                  description: failureText(problemOf(error)),
                }),
            })
          }
        >
          {target.note && (
            <p className="text-small text-muted-foreground">{target.note}</p>
          )}
        </Dialog>
      )}
    </section>
  );
}
