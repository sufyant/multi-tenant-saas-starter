"use client";

import {
  getListRolesQueryOptions,
  Permission,
  type RoleDetailResponse,
  schemas,
} from "@repo/api-client";
import { useQuery } from "@tanstack/react-query";
import { useCan, useTenant } from "./tenant-context";

/** The escalation rule (F51): the viewer's permissions hold every permission of the role. */
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

/** `canActOn(roleId)`: `holdsAll` for a role of the tenant's roles; false for an id they do not hold (F51). */
export function useCanActOn(): (roleId: string) => boolean {
  const { detail } = useTenant();
  const roles = useTenantRoles();
  return (roleId) => {
    const role = roles.data?.items.find((item) => item.id === roleId);
    return role ? holdsAll(detail.permissions, role) : false;
  };
}
