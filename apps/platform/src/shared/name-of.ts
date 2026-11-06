import { type UserResponse, UserResponseStatus } from "@repo/api-client";

/**
 * A person's whole name, or null when they have none: a deleted user's name is erased (A8), and the screen shows "Deleted user" in
 * its own language (K3).
 */
export const nameOf = (person: { name: string | null }) => person.name || null;

/**
 * The name every screen shows for a user: "Deleted user" once the user is deleted, whatever name an answer still holds (A8);
 * else their name; else their email, as a root admin whom the door added has no name yet (A7).
 */
export const userNameOf = (
  user: Pick<UserResponse, "name" | "email" | "status">,
  deletedUser: string,
) =>
  user.status === UserResponseStatus.Deleted
    ? deletedUser
    : (nameOf(user) ?? user.email ?? "");
