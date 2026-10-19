import {
  actionFor,
  ErrorCode,
  getGetMeQueryKey,
  getGetMyTenantQueryKey,
  getListInvitationsQueryKey,
  getListMembersQueryKey,
  getListMyTenantsQueryKey,
  ProblemDetailsError,
} from "@repo/api-client";
import type { QueryClient } from "@tanstack/react-query";

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

/**
 * What a refused command means beyond its dialog (F4): a 401 goes to sign-in; a suspended user reads GetMe again, and the shell
 * shows the suspended screen; an organisation that is suspended, closed or no longer the caller's is read again, and the tenant
 * layout shows its page.
 */
export function afterRefusal(
  queryClient: QueryClient,
  tenantId: string,
  error: unknown,
): void {
  if (!(error instanceof ProblemDetailsError)) return;
  const action = actionFor(error.status, error.problem);
  if (action.kind === "sign-in") window.location.assign("/sign-in");
  if (action.kind === "account-suspended")
    void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
  if (error.problem && tenantGone.has(error.problem.code))
    void refreshTenant(queryClient, tenantId);
}
