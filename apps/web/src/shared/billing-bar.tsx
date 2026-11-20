"use client";

import {
  getGetMeQueryOptions,
  getGetPlanChoicesQueryOptions,
  MeterSettingResponseOverage,
  PaymentResponseState,
  type UsageResponse,
} from "@repo/api-client";
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
 * The billing bar above every page but Billing's own, for a viewer who manages billing (the Billing bar priority pattern; 2.32,
 * 2.33): a failed payment while Stripe retries, with "Update card", and the last warning in billing's grace, with "Pay now"; both
 * open Stripe's portal. The connection bar comes first (F59), so this one gives way while the server does not answer. Without a
 * payment's notice it tells of the meters (`UsageNotice`, 2.34 to 2.37). The trial's bar (2.31) comes with step 40d.
 */
export function BillingBar({
  onBilling,
  onUsage,
}: {
  onBilling: boolean;
  onUsage: boolean;
}) {
  const t = useTranslations();
  const { tenant } = useTenant();
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
  if (!paymentShown) return onUsage ? null : <UsageNotice />;
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
 * The meters' notice, the most urgent first: the spending cap reached (2.37), a meter that stops used up (2.36), a charged meter past
 * its quota (2.35), a meter at 80% of it (2.34). It reads GetUsage, which answers only a viewer who manages billing (F66).
 */
function UsageNotice() {
  const usage = useUsage();
  if (!usage.data || usage.data.meters.length === 0) return null;
  return <UsageNoticeOf usage={usage.data} />;
}

function UsageNoticeOf({ usage }: { usage: UsageResponse }) {
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
