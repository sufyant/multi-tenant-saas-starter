import {
  ErrorCode,
  getGetBillingQueryOptions,
  Permission,
  problemOf,
} from "@repo/api-client";
import { useQuery } from "@tanstack/react-query";
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
