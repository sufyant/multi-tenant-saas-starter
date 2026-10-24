import {
  BuiltInRole,
  builtInRoleOf,
  type UserMembershipResponse,
} from "@repo/api-client";

/**
 * Whether a membership is an Owner's, so Make Owner is disabled on it (the UserDetail board). The platform holds no tenant
 * permissions to compare (F51 is the tenant web's rule), and the API's own rule knows the Owner role the same way: by the
 * built-in Owner role, 409 `member.already_owner`. This is the platform's one decision by a role's name (F3), and the F51 check
 * skips only this file and `use-role-name.ts`.
 */
export const isOwnerMembership = (membership: UserMembershipResponse) =>
  builtInRoleOf(membership.role) === BuiltInRole.Owner;
