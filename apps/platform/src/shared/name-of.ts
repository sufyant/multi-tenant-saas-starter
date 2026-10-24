import { type UserResponse, UserResponseStatus } from "@repo/api-client";

/**
 * A person's names as one, or null when they have none: a deleted user's names are erased (A8), and the screen shows "Deleted
 * user" in its own language (K3).
 */
export const nameOf = (person: {
  firstName: string | null;
  lastName: string | null;
}) => [person.firstName, person.lastName].filter(Boolean).join(" ") || null;

/**
 * The name every screen shows for a user: "Deleted user" once the user is deleted, whatever names an answer still holds (A8);
 * else their names; else their email, as a root admin whom the door added has no names yet (A7).
 */
export const userNameOf = (
  user: Pick<UserResponse, "firstName" | "lastName" | "email" | "status">,
  deletedUser: string,
) =>
  user.status === UserResponseStatus.Deleted
    ? deletedUser
    : (nameOf(user) ?? user.email ?? "");
