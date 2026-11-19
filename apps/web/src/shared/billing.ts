import {
  BillingResponseBilledBy,
  ErrorCode,
  getGetBillingQueryOptions,
  getGetPlanChoicesQueryOptions,
  Permission,
  problemOf,
  SubscriptionResponseInterval,
  SubscriptionResponseStatus,
} from "@repo/api-client";
import { formatMoney } from "@repo/ui/lib/format-money";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useCan, useTenant } from "./tenant-context";

/** The API answers 404 `billing.off` for every billing call while the deployment's billing is off (the API's B1). */
export const isBillingOff = (error: unknown): boolean =>
  problemOf(error)?.code === ErrorCode.BillingOff;

/**
 * GetBilling of the tenant, for a viewer who manages billing; disabled for anybody else. The settings menu, Home and the Billing page
 * read the same query, so the organisation's billing costs one request.
 */
export function useBilling() {
  const can = useCan();
  const { tenant } = useTenant();
  return useQuery({
    ...getGetBillingQueryOptions(tenant.id),
    enabled: can(Permission.TenantBillingManage),
  });
}

/**
 * Whether Billing is a page of this organisation for the viewer: they manage billing and GetBilling answered. When billing is off the
 * page is in no menu, so a template without billing shows no Billing (B1); while GetBilling loads it is only in the menu of its own
 * page (10.20), so the other pages do not show it for a moment. Another failure keeps it, so the page can say what failed (10.21).
 */
export function useBillingShown(onBilling = false): boolean {
  const billing = useBilling();
  if (billing.isSuccess) return true;
  if (billing.isPending) return onBilling && billing.fetchStatus !== "idle";
  return !isBillingOff(billing.error);
}

/**
 * How a limit's dialog offers more (8.39, 8.40, 8.43, 8.45, 9.14, 9.19): "change", Change plan, for a viewer who manages billing
 * while the organisation pays by card; "request", the brand's support contact, while billing is off, for a Contract organisation,
 * and while GetBilling is not known.
 */
export function usePlanChange(): "change" | "request" {
  const billing = useBilling();
  return billing.data?.billedBy === BillingResponseBilledBy.Card
    ? "change"
    : "request";
}

/**
 * The monthly fee per person that a seat adds to the bill, as text, for a viewer who manages billing of an organisation that pays
 * monthly by card (14.02, 14.06, 14.07); null otherwise. A yearly payer is charged the rest of the year at once (14.04), an amount
 * that only Stripe's proration knows, so no line says it.
 */
export function useSeatPrice(): string | null {
  const t = useTranslations();
  const { tenant } = useTenant();
  const billing = useBilling();
  const subscription = billing.data?.subscription;
  const monthly =
    billing.data?.billedBy === BillingResponseBilledBy.Card &&
    subscription?.status === SubscriptionResponseStatus.Active &&
    subscription.interval === SubscriptionResponseInterval.Monthly;
  const choices = useQuery({
    ...getGetPlanChoicesQueryOptions(tenant.id),
    enabled: monthly,
  });
  const plan = choices.data?.plans.find(
    (choice) => choice.id === subscription?.planId,
  );
  if (!monthly || !plan || plan.prices.perPersonMonthly === 0) return null;
  return formatMoney(
    plan.prices.perPersonMonthly,
    plan.currency,
    t("locale.format"),
  );
}
