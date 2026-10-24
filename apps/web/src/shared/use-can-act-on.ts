"use client";

import {
  getListRolesQueryOptions,
  Permission,
  type RoleDetailResponse,
  schemas,
} from "@repo/api-client";
import { useQuery } from "@tanstack/react-query";
import { useCan, useTenant } from "./tenant-context";

/**
 * The escalation rule of the API (F51): the viewer acts on a role only when their own role holds every permission of it. It is
 * true for an empty role, and false for a role with one permission more than the viewer holds.
 */
export const holdsAll = (
  viewerPermissions: readonly Permission[],
  role: Pick<RoleDetailResponse, "permissions">,
): boolean =>
  role.permissions.every((permission) =>
    viewerPermissions.includes(permission),
  );

/** The tenant's roles (ListRoles) on one page: the built-in roles and the few custom roles a tenant may have. */
export const listRolesOptions = (tenantId: string, enabled = true) =>
  getListRolesQueryOptions(
    tenantId,
    { pageSize: schemas.listRolesQueryPageSizeMax },
    { query: { enabled } },
  );

/** The tenant's roles with their permissions, for a viewer who may read them (`tenant.roles.read`). */
export function useTenantRoles() {
  const { tenant } = useTenant();
  const can = useCan();
  return useQuery(listRolesOptions(tenant.id, can(Permission.TenantRolesRead)));
}

/**
 * `canActOn(roleId)`: the viewer may give the role, or act on a member or an invitation that has it (F51). It reads
 * GetMyTenant.permissions and the tenant's roles. A role id that ListRoles does not know answers false. The API still decides.
 */
export function useCanActOn(): (roleId: string) => boolean {
  const { detail } = useTenant();
  const roles = useTenantRoles();
  return (roleId) => {
    const role = roles.data?.items.find((item) => item.id === roleId);
    return role ? holdsAll(detail.permissions, role) : false;
  };
}
