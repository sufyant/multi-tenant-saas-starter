"use client";

import {
  getGetMeQueryOptions,
  getGetPlanChoicesQueryOptions,
  PaymentResponseState,
} from "@repo/api-client";
import { formatDate } from "@repo/ui/lib/format-date-time";
import { cn } from "@repo/ui/lib/utils";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useBilling } from "./billing";
import { useServerReach } from "./server-bar";
import { useTenant } from "./tenant-context";
import { usePortal } from "./use-portal";

/**
 * The billing bar above every page but Billing's own, for a viewer who manages billing (the Billing bar priority pattern; 2.32,
 * 2.33): a failed payment while Stripe retries, with "Update card", and the last warning in billing's grace, with "Pay now"; both
 * open Stripe's portal. The connection bar comes first (F59), so this one gives way while the server does not answer. The trial's
 * bar (2.31) comes with step 40d and the meters' (2.34 to 2.37) with step 40c.
 */
export function BillingBar({ onBilling }: { onBilling: boolean }) {
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
  if (serverDown || onBilling || !payment) return null;
  if (payment.state === PaymentResponseState.MovedToDefault) return null;
  const fallback = choices.data?.plans.find((plan) => plan.isDefault);
  if (grace && !fallback) return null;
  return (
    <div
      data-slot="billing-bar"
      className={cn(
        "flex min-h-8 shrink-0 flex-wrap items-center justify-center gap-x-2 px-4 py-1 text-center text-small font-medium",
        grace
          ? "bg-destructive-subtle text-destructive-text"
          : "bg-warning-subtle text-warning-text",
      )}
    >
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
        className="rounded-sm underline underline-offset-4 outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-45"
      >
        {grace ? t("billing.payNow") : t("billing.updateCard")}
      </button>
    </div>
  );
}
