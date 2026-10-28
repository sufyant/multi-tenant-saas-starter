import {
  actionFor,
  ErrorCode,
  getGetMeQueryKey,
  getGetMyTenantQueryKey,
  getListInvitationsQueryKey,
  getListMembersQueryKey,
  getListMyTenantsQueryKey,
  getListRolesQueryKey,
  ProblemDetailsError,
} from "@repo/api-client";
import type { QueryClient } from "@tanstack/react-query";

// After a change the screen waits for the server and then reads again (F47): it invalidates the lists by their generated keys.
// No optimistic update: the API owns the Owner rules (K1).

/** The tenant's counts of its limits (`GetMyTenant.usage`), which a member or an invitation added or removed changes. */
const refreshUsage = (queryClient: QueryClient, tenantId: string) =>
  queryClient.invalidateQueries({ queryKey: getGetMyTenantQueryKey(tenantId) });

export const refreshMembers = (queryClient: QueryClient, tenantId: string) =>
  Promise.all([
    queryClient.invalidateQueries({
      queryKey: getListMembersQueryKey(tenantId),
    }),
    refreshUsage(queryClient, tenantId),
  ]);

export const refreshInvitations = (
  queryClient: QueryClient,
  tenantId: string,
) =>
  Promise.all([
    queryClient.invalidateQueries({
      queryKey: getListInvitationsQueryKey(tenantId),
    }),
    refreshUsage(queryClient, tenantId),
  ]);

/** The caller's own role and permissions: after a change of their own role, or when the organisation stopped. */
export const refreshTenant = (queryClient: QueryClient, tenantId: string) =>
  Promise.all([
    queryClient.invalidateQueries({
      queryKey: getGetMyTenantQueryKey(tenantId),
    }),
    queryClient.invalidateQueries({ queryKey: getListMyTenantsQueryKey() }),
  ]);

/** The roles, their permissions and the lists that show them: after a refusal that says the roles changed meanwhile (F51). */
export const refreshRoles = (queryClient: QueryClient, tenantId: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getListRolesQueryKey(tenantId) }),
    queryClient.invalidateQueries({
      queryKey: getGetMyTenantQueryKey(tenantId),
    }),
    refreshMembers(queryClient, tenantId),
    refreshInvitations(queryClient, tenantId),
  ]);

const rolesChanged = new Set<string>([
  ErrorCode.RoleEscalation,
  ErrorCode.RoleNotFound,
]);

const planChanged = new Set<string>([
  ErrorCode.PlanFeatureUnavailable,
  ErrorCode.PlanLimitReached,
]);

const tenantGone = new Set<string>([
  ErrorCode.TenantSuspended,
  ErrorCode.TenantClosed,
  ErrorCode.TenantNotFound,
]);

/**
 * What a refused command means beyond its dialog (F4): a 401 goes to sign-in; a suspended user reads GetMe again, and the shell
 * shows the suspended screen; an organisation that is suspended, closed or no longer the caller's is read again, and the tenant
 * layout shows its page; a role that changed or went meanwhile (`role.escalation`, `role.not_found`) reads the roles, the viewer's
 * permissions and the lists again, so the dialog and the menus follow (F47, F51); a refusal of the plan (`plan.feature_unavailable`,
 * `plan.limit_reached`) reads GetMyTenant again, so the counts and the closed actions follow the plan as it is now.
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
  if (error.problem && rolesChanged.has(error.problem.code))
    void refreshRoles(queryClient, tenantId);
  if (error.problem && planChanged.has(error.problem.code))
    void refreshUsage(queryClient, tenantId);
}
