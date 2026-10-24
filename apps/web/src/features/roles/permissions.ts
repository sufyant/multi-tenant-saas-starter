import { Permission } from "@repo/api-client";

/**
 * The tenant pool in the groups and the order of the roles screen (the RoleCreate board): Organisation, Members, Invitations,
 * Roles, Activity. Manage Owners closes the Members group; only the Owner holds it (K1), so only the Owner's page shows it.
 */
export const permissionGroups = [
  { id: "organisation", permissions: [Permission.TenantProfileRead] },
  {
    id: "members",
    permissions: [
      Permission.TenantMembersRead,
      Permission.TenantMembersUpdate,
      Permission.TenantMembersDelete,
      Permission.TenantOwnersManage,
    ],
  },
  {
    id: "invitations",
    permissions: [
      Permission.TenantInvitationsRead,
      Permission.TenantInvitationsCreate,
      Permission.TenantInvitationsDelete,
    ],
  },
  {
    id: "roles",
    permissions: [
      Permission.TenantRolesRead,
      Permission.TenantRolesCreate,
      Permission.TenantRolesUpdate,
      Permission.TenantRolesDelete,
    ],
  },
  { id: "activity", permissions: [Permission.TenantAuditRead] },
] as const satisfies readonly {
  id: string;
  permissions: readonly Permission[];
}[];

/** The permissions that need See roles: a role that gives, changes or takes a role reads the roles (the API's r12). */
export const needRolesRead: readonly Permission[] = [
  Permission.TenantInvitationsCreate,
  Permission.TenantMembersUpdate,
  Permission.TenantMembersDelete,
  Permission.TenantInvitationsDelete,
];

/** Why a permission of the editor is checked and cannot be unchecked, or null when the person may change it. */
export type Lock = "everyRole" | "rolesReadNeeded" | null;

export const lockOf = (
  permission: Permission,
  held: readonly Permission[],
): Lock => {
  if (permission === Permission.TenantProfileRead) return "everyRole";
  if (
    permission === Permission.TenantRolesRead &&
    needRolesRead.some((need) => held.includes(need))
  )
    return "rolesReadNeeded";
  return null;
};

/**
 * The permissions a role holds after the editor's rules: every role sees the organisation's details, and a role that needs See
 * roles has it. The order is the screen's, so a request and a comparison do not depend on the order of the clicks.
 */
export const withRequired = (held: readonly Permission[]): Permission[] => {
  const all = new Set(held);
  all.add(Permission.TenantProfileRead);
  if (needRolesRead.some((need) => all.has(need)))
    all.add(Permission.TenantRolesRead);
  return permissionGroups
    .flatMap((group): readonly Permission[] => group.permissions)
    .filter((permission) => all.has(permission));
};
