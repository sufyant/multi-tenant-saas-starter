"use client";

import {
  getGetMeQueryOptions,
  getGetPlanChoicesQueryOptions,
  MeterSettingResponseOverage,
  PaymentResponseState,
  type UsageResponse,
} from "@repo/api-client";
import { daysLeft } from "@repo/ui/lib/days-left";
import { formatDate } from "@repo/ui/lib/format-date-time";
import { cn } from "@repo/ui/lib/utils";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { useBilling, usePlanChange } from "./billing";
import { useServerReach } from "./server-bar";
import { useTenant } from "./tenant-context";
import { useUsage } from "./usage";
import { usePortal } from "./use-portal";
import { useUsageText } from "./use-usage-text";

/**
 * The billing bar, for a viewer who manages billing (the Billing bar priority pattern). Above every page but Billing's own (2.32,
 * 2.33): a failed payment while Stripe retries, with "Update card", and the last warning in billing's grace, with "Pay now"; both
 * open Stripe's portal. The connection bar comes first (F59), so this one gives way while the server does not answer. Without a
 * payment's notice it tells of the meters above every page but Usage's, Billing's too (`UsageNotice`, 2.34 to 2.37), and of a running trial
 * that no payment ended above every page but Billing's (`TrialNotice`, 1.31, 2.31). The trial comes after a meter used up and before a
 * meter at 80%, as the Billing bar's priority says.
 */
export function BillingBar({
  onBilling,
  onUsage,
}: {
  onBilling: boolean;
  onUsage: boolean;
}) {
  const t = useTranslations();
  const { tenant, detail } = useTenant();
  const billing = useBilling();
  const { serverDown } = useServerReach();
  const portal = usePortal();
  const payment = billing.data?.subscription?.payment ?? null;
  const grace = payment?.state === PaymentResponseState.Grace;
  const choices = useQuery({
    ...getGetPlanChoicesQueryOptions(tenant.id),
    enabled: grace,
  });
  const me = useQuery(getGetMeQueryOptions());
  const date = (at: string) =>
    formatDate(at, { locale: t("locale.format"), timeZone: me.data?.timeZone });
  if (serverDown) return null;
  const fallback = choices.data?.plans.find((plan) => plan.isDefault);
  const paymentShown =
    !onBilling &&
    payment !== null &&
    payment.state !== PaymentResponseState.MovedToDefault &&
    (!grace || fallback !== undefined);
  const trial =
    !onBilling && billing.data && !billing.data.subscription
      ? detail.trial
      : null;
  const trialNotice = trial?.returnsTo ? (
    <TrialNotice
      plan={trial.plan.name}
      endsAt={trial.endsAt}
      returnsTo={trial.returnsTo.name}
      date={date}
    />
  ) : null;
  if (!paymentShown)
    return onUsage ? trialNotice : <UsageNotice trial={trialNotice} />;
  return (
    <Bar tone={grace ? "destructive" : "warning"}>
      <span role="status">
        {grace
          ? t("billing.grace", {
              default: fallback?.name ?? "",
              date: date(payment.graceEndsAt ?? payment.failedAt),
            })
          : t("billing.retrying", {
              date: date(payment.nextAttemptAt ?? payment.failedAt),
            })}
      </span>
      <span aria-hidden="true">·</span>
      <button
        type="button"
        disabled={portal.pending}
        onClick={portal.open}
        className={cn(barLink, "disabled:opacity-45")}
      >
        {grace ? t("billing.payNow") : t("billing.updateCard")}
      </button>
    </Bar>
  );
}

const barLink =
  "rounded-sm underline underline-offset-4 outline-none focus-visible:ring-2 focus-visible:ring-ring";

function Bar({
  tone,
  children,
}: {
  tone: "info" | "warning" | "destructive";
  children: ReactNode;
}) {
  return (
    <div
      data-slot="billing-bar"
      className={cn(
        "flex min-h-8 shrink-0 flex-wrap items-center justify-center gap-x-2 px-4 py-1 text-center text-small font-medium",
        tone === "info" && "bg-info-subtle text-info-text",
        tone === "warning" && "bg-warning-subtle text-warning-text",
        tone === "destructive" && "bg-destructive-subtle text-destructive-text",
      )}
    >
      {children}
    </div>
  );
}

/**
 * A running trial (1.31, 2.31): its end, and 3 days before it the days left and the plan that comes after it, with Choose a plan,
 * which opens Change plan. The days left count a part of a day as a day, as Billing does.
 */
function TrialNotice({
  plan,
  endsAt,
  returnsTo,
  date,
}: {
  plan: string;
  endsAt: string;
  returnsTo: string;
  date: (at: string) => string;
}) {
  const t = useTranslations();
  const { tenant } = useTenant();
  const days = daysLeft(endsAt);
  return (
    <Bar tone="info">
      <span role="status">
        {days <= trialSoonDays
          ? t("billing.trialBarSoon", {
              plan,
              count: days,
              organisation: tenant.name,
              returnsTo,
            })
          : t("billing.trialBar", { plan, date: date(endsAt) })}
      </span>
      <span aria-hidden="true">·</span>
      <Link href={`/${tenant.slug}/settings/billing/plans`} className={barLink}>
        {t("billing.choosePlan")}
      </Link>
    </Bar>
  );
}

/** The days before a trial's end from which its bar says the days left, as the trial's reminder comes then (13.16). */
const trialSoonDays = 3;

/**
 * The meters' notice, the most urgent first: the spending cap reached (2.37), a meter that stops used up (2.36), a charged meter past
 * its quota (2.35), then the trial's notice when there is one, then a meter at 80% of it (2.34). It reads GetUsage, which answers only
 * a viewer who manages billing (F66).
 */
function UsageNotice({ trial }: { trial: ReactNode }) {
  const usage = useUsage();
  if (!usage.data || usage.data.meters.length === 0) return trial;
  return <UsageNoticeOf usage={usage.data} trial={trial} />;
}

function UsageNoticeOf({
  usage,
  trial,
}: {
  usage: UsageResponse;
  trial: ReactNode;
}) {
  const t = useTranslations();
  const { tenant } = useTenant();
  const text = useUsageText(usage);
  const planChange = usePlanChange();
  const usagePage = `/${tenant.slug}/settings/usage`;
  const link = (href: string, label: string) => (
    <>
      <span aria-hidden="true">·</span>
      <Link href={href} className={barLink}>
        {label}
      </Link>
    </>
  );
  const viewUsage = link(usagePage, t("usage.viewUsage"));
  const charged = usage.meters.filter(
    (meter) => meter.overage === MeterSettingResponseOverage.Charge,
  );
  if (usage.spendingCapOffered && usage.spendingCap?.reachedAt)
    return (
      <Bar tone="destructive">
        <span role="status">
          {t("usage.barCapReached", {
            meter:
              charged.length === 1 && charged[0]
                ? text.meterName(charged[0].key)
                : t("usage.chargedMeters"),
            amount: text.money(usage.spendingCap.amount),
            date: text.resetsOn,
          })}
        </span>
        {link(usagePage, t("usage.changeCap"))}
      </Bar>
    );
  const usedUp = usage.meters.find(
    (meter) =>
      meter.overage === MeterSettingResponseOverage.Stop &&
      meter.used >= meter.quota,
  );
  if (usedUp)
    return (
      <Bar tone="destructive">
        <span role="status">
          {t("usage.usedUpTitle", {
            quota: text.count(usedUp.quota),
            meter: text.meterName(usedUp.key),
            date: text.resetsOn,
          })}
        </span>
        {planChange === "change"
          ? link(
              `/${tenant.slug}/settings/billing/plans`,
              t("usage.changePlan"),
            )
          : viewUsage}
      </Bar>
    );
  const over = charged.find((meter) => meter.used >= meter.quota);
  if (over)
    return (
      <Bar tone="warning">
        <span role="status">
          {t("usage.barCharging", {
            quota: text.count(over.quota),
            meter: text.meterName(over.key),
            price: text.money(over.unitPrice),
            size: text.count(over.unitSize),
          })}
        </span>
        {viewUsage}
      </Bar>
    );
  if (trial) return trial;
  const nearly = usage.meters.find(
    (meter) => meter.quota > 0 && meter.used >= meter.quota * 0.8,
  );
  if (!nearly) return null;
  return (
    <Bar tone="info">
      <span role="status">
        {t("usage.barNearly", {
          meter: text.meterName(nearly.key),
          used: text.count(nearly.used),
          quota: text.count(nearly.quota),
        })}
      </span>
      {viewUsage}
    </Bar>
  );
}
