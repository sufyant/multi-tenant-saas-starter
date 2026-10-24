import type { RoleResponse } from "./generated/endpoints.schemas.gen";

/**
 * The built-in roles by their stored names. The API answers a built-in role with `builtIn` true and one of these names
 * (`RoleResponse`); a custom role has `builtIn` false and a name of its own. This is the one place that writes these names (F3).
 */
export const BuiltInRole = {
  Owner: "owner",
  Admin: "admin",
  Member: "member",
} as const;

export type BuiltInRole = (typeof BuiltInRole)[keyof typeof BuiltInRole];

const builtInRoles = Object.values(BuiltInRole);

/** The built-in role a role is, or null for a custom role, whatever its name. */
export const builtInRoleOf = (
  role: Pick<RoleResponse, "name" | "builtIn">,
): BuiltInRole | null =>
  (role.builtIn && builtInRoles.find((name) => name === role.name)) || null;
