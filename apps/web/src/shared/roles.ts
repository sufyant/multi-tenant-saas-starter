import type { RoleResponse } from "@repo/api-client";

/**
 * The built-in roles by their stored names. The API answers a built-in role with `builtIn` true and one of these names; a custom
 * role has `builtIn` false and a name of its own. This is the one place of the web that writes these names.
 */
export const BuiltInRole = {
  Owner: "owner",
  Admin: "admin",
  Member: "member",
} as const;

export type BuiltInRole = (typeof BuiltInRole)[keyof typeof BuiltInRole];

type Role = Pick<RoleResponse, "name" | "builtIn">;

const builtInRoles = Object.values(BuiltInRole);

/**
 * The built-in role a role is, or null for a custom role. Only the text of a role (`use-role-name.ts`), the list order and the
 * invite dialog's first choice read it. A role's name decides nothing: the permissions do (F51).
 */
export const builtInRoleOf = (role: Role): BuiltInRole | null =>
  (role.builtIn && builtInRoles.find((name) => name === role.name)) || null;

/** The roles in the order a list shows them: Owner, Admin and Member first, then the custom roles in the order they came. */
export const inListOrder = <T extends Role>(roles: readonly T[]): T[] => {
  const rank = (role: T) => {
    const builtIn = builtInRoleOf(role);
    return builtIn ? builtInRoles.indexOf(builtIn) : builtInRoles.length;
  };
  return [...roles].sort((a, b) => rank(a) - rank(b));
};

/**
 * Whether another role of the tenant has this name, as the API counts it (409 `role.name_taken`): trimmed, without regard to
 * case, the built-in names included. A name's uniqueness is a fact of names; it gives no role any access (F51).
 */
export const nameTaken = (
  roles: readonly (Role & { id: string })[],
  name: string,
  exceptId?: string,
) => {
  const wanted = name.trim().toLowerCase();
  return roles.some(
    (role) => role.id !== exceptId && role.name.toLowerCase() === wanted,
  );
};

/** The role the invite dialog starts with: the built-in Member, the role with the least access. */
export const firstInviteRole = <T extends Role>(roles: readonly T[]) =>
  roles.find((role) => builtInRoleOf(role) === BuiltInRole.Member);
