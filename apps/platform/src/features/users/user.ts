import {
  actionFor,
  ErrorCode,
  getGetMeQueryKey,
  getGetUserQueryKey,
  getListUsersQueryKey,
  ProblemDetailsError,
  type UserResponse,
  UserResponseStatus,
} from "@repo/api-client";
import type { StatusTone } from "@repo/ui/components/status";
import type { QueryClient } from "@tanstack/react-query";

/** What the list and the user's page both know of a user: an item of ListUsers, or the answer of GetUser. */
export type User = Pick<
  UserResponse,
  "id" | "email" | "firstName" | "lastName" | "status"
>;

/** The message key and the tone of each status (the Users board). Colour is never the only signal: the word stays. */
export const statuses = {
  [UserResponseStatus.Active]: { label: "users.statusActive", tone: "success" },
  [UserResponseStatus.Suspended]: {
    label: "users.statusSuspended",
    tone: "warning",
  },
  [UserResponseStatus.Deleted]: {
    label: "users.statusDeleted",
    tone: "neutral",
  },
} as const satisfies Record<
  UserResponseStatus,
  { label: string; tone: StatusTone }
>;

/**
 * A person's names as one, or null when they have none: a deleted user's names are erased (A8), and the screen shows "Deleted
 * user" in its own language (K3).
 */
export const nameOf = (person: {
  firstName: string | null;
  lastName: string | null;
}) => [person.firstName, person.lastName].filter(Boolean).join(" ") || null;

/** A deleted user takes no action and has no name (A8). */
export const isDeleted = (user: Pick<User, "status">) =>
  user.status === UserResponseStatus.Deleted;

export type Action = "rename" | "suspend" | "reactivate" | "delete";

/**
 * The actions of a user by their status (the Users board): Rename, Suspend or Reactivate, then Delete; none for a deleted user.
 * `null` is a separator. Every system admin holds every platform permission (the API's A4), so the menu has no permission check;
 * the API still decides.
 */
export function actionsOf(user: User): (Action | null)[] {
  switch (user.status) {
    case UserResponseStatus.Active:
      return ["rename", "suspend", null, "delete"];
    case UserResponseStatus.Suspended:
      return ["rename", "reactivate", null, "delete"];
    default:
      return [];
  }
}

/** After a command the screen reads again (F47): the user, and every page of the list. */
export const refreshUser = (queryClient: QueryClient, id: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getGetUserQueryKey(id) }),
    queryClient.invalidateQueries({ queryKey: getListUsersQueryKey() }),
  ]);

/** A refusal that says the user or a membership changed meanwhile: the dialog shows its text, and the screen reads again (F47). */
const changedMeanwhile = new Set<string>([
  ErrorCode.UserNotFound,
  ErrorCode.UserDeleted,
  ErrorCode.UserSuspended,
  ErrorCode.UserIsSystemAdmin,
  ErrorCode.UserLastOwner,
  ErrorCode.UserHasMemberships,
  ErrorCode.MemberAlreadyOwner,
  ErrorCode.MemberNotFound,
  ErrorCode.TenantNotActive,
  ErrorCode.TenantNotFound,
]);

/**
 * What a refused command means beyond its dialog (F4, F47): a 401 goes to sign-in; the caller's own suspension or deletion (403)
 * reads GetMe again, and the shell shows the account's screen; a conflict reads the user and the list again. A 409
 * `user.suspended` or `user.deleted` is about the user the command names, not the caller.
 */
export function afterRefusal(
  queryClient: QueryClient,
  id: string,
  error: unknown,
): void {
  if (!(error instanceof ProblemDetailsError)) return;
  const { kind } = actionFor(error.status, error.problem);
  if (kind === "sign-in") window.location.assign("/sign-in");
  if (kind === "account-suspended" || kind === "account-deleted") {
    void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
    return;
  }
  if (error.problem && changedMeanwhile.has(error.problem.code))
    void refreshUser(queryClient, id);
}

/** The name a screen shows for a user: "Deleted user" once the user is deleted, whatever names an answer still holds (A8). */
export const displayNameOf = (user: User, deletedUser: string) =>
  (!isDeleted(user) && nameOf(user)) || deletedUser;
