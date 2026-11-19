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
 * Whether Billing is a page of this organisation for the viewer: they manage billing and GetBilling answered. While it loads, and when
 * billing is off, the page is in no menu, so a template without billing shows no Billing (B1). Another failure keeps it, so the
 * page can say what failed.
 */
export function useBillingShown(): boolean {
  const billing = useBilling();
  if (billing.isSuccess) return true;
  return billing.isError && !isBillingOff(billing.error);
}
