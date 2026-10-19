import {
  ErrorCode,
  getGetMyTenantQueryKey,
  getListInvitationsQueryKey,
  getListMembersQueryKey,
  getListMyTenantsQueryKey,
} from "@repo/api-client";
import type { QueryClient } from "@tanstack/react-query";
import { problemOf } from "./problem";

// After a change the screen waits for the server and then reads again (F47): it invalidates the lists by their generated keys.
// No optimistic update: the API owns the Owner rules (K1).

export const refreshMembers = (queryClient: QueryClient, tenantId: string) =>
  queryClient.invalidateQueries({ queryKey: getListMembersQueryKey(tenantId) });

export const refreshInvitations = (
  queryClient: QueryClient,
  tenantId: string,
) =>
  queryClient.invalidateQueries({
    queryKey: getListInvitationsQueryKey(tenantId),
  });

/** The caller's own role and permissions: after a change of their own role, or when the organisation stopped. */
export const refreshTenant = (queryClient: QueryClient, tenantId: string) =>
  Promise.all([
    queryClient.invalidateQueries({
      queryKey: getGetMyTenantQueryKey(tenantId),
    }),
    queryClient.invalidateQueries({ queryKey: getListMyTenantsQueryKey() }),
  ]);

const tenantGone = new Set<string>([
  ErrorCode.TenantSuspended,
  ErrorCode.TenantClosed,
  ErrorCode.TenantNotFound,
]);

/** The organisation is suspended, closed or no longer the caller's: the tenant layout reads it again and shows its page. */
export function refreshWhenTenantGone(
  queryClient: QueryClient,
  tenantId: string,
  error: unknown,
): void {
  const code = problemOf(error)?.code;
  if (code && tenantGone.has(code)) void refreshTenant(queryClient, tenantId);
}
