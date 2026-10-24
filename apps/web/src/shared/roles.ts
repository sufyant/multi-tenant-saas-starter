import {
  BuiltInRole,
  builtInRoleOf,
  type RoleResponse,
} from "@repo/api-client";

// The built-in role names live in `packages/api-client/src/roles.ts` (F3). This file holds only what the web does with them: the
// list order, a name's uniqueness and the invite dialog's first role. A role's name decides nothing: the permissions do (F51).

type Role = Pick<RoleResponse, "name" | "builtIn">;

const builtInRoles = Object.values(BuiltInRole);

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
