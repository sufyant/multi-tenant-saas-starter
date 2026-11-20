import { getGetUsageQueryOptions, Permission } from "@repo/api-client";
import { useQuery } from "@tanstack/react-query";
import { useBillingShown } from "./billing";
import { useCan, useTenant } from "./tenant-context";

/**
 * GetUsage of the tenant, for a viewer who manages billing; disabled for anybody else. The settings menu, the billing bar and the Usage
 * page read the same query.
 */
export function useUsage() {
  const can = useCan();
  const { tenant } = useTenant();
  return useQuery({
    ...getGetUsageQueryOptions(tenant.id),
    enabled: can(Permission.TenantBillingManage),
  });
}

/**
 * Whether Usage is a page of this organisation for the viewer (F66): Billing is (`useBillingShown`), and the plan has a meter, as the
 * template ships none. While GetUsage loads it is only in the menu of its own page; a failure keeps it, so the page can say what failed.
 */
export function useUsageShown(onUsage = false): boolean {
  const billing = useBillingShown(onUsage);
  const usage = useUsage();
  if (!billing) return false;
  if (usage.isSuccess) return usage.data.meters.length > 0;
  if (usage.isPending) return onUsage && usage.fetchStatus !== "idle";
  return true;
}
