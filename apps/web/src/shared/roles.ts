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

/** The built-in role a role is, or null for a custom role. */
export const builtInRoleOf = (role: Role): BuiltInRole | null =>
  (role.builtIn && builtInRoles.find((name) => name === role.name)) || null;

/** The role is the built-in Owner role (K1: only an Owner gives or takes it). */
export const isOwner = (role: Role) =>
  builtInRoleOf(role) === BuiltInRole.Owner;

/** The roles in the order a list shows them: Owner, Admin and Member first, then the custom roles in the order they came. */
export const inListOrder = <T extends Role>(roles: readonly T[]): T[] => {
  const rank = (role: T) => {
    const builtIn = builtInRoleOf(role);
    return builtIn ? builtInRoles.indexOf(builtIn) : builtInRoles.length;
  };
  return [...roles].sort((a, b) => rank(a) - rank(b));
};
