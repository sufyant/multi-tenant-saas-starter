import {
  actionFor,
  ErrorCode,
  getGetMeQueryKey,
  getGetUserQueryKey,
  getListUsersInfiniteQueryKey,
  getListUsersQueryKey,
  ProblemDetailsError,
  type UserResponse,
  UserResponseStatus,
} from "@repo/api-client";
import type { PillTone } from "@repo/ui/components/pill";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import type { QueryClient } from "@tanstack/react-query";

/** What the list and the user's page both know of a user: an item of ListUsers, or the answer of GetUser. */
export type User = Pick<
  UserResponse,
  "id" | "email" | "name" | "status" | "isSystemAdmin"
>;

/** The message key and the Pill's tone of each status (the Pill component). Colour is never the only signal: the word stays. */
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
  { label: string; tone: PillTone }
>;

export type Action = "rename" | "suspend" | "reactivate" | "delete";

/**
 * The actions of a user by their status (the Users board): Rename, Suspend or Reactivate, then Delete; none for a deleted user.
 * The menu puts its separator before Delete. Every system admin holds every platform permission (the API's A4), so the menu has no permission check;
 * the API still decides.
 */
export function actionsOf(user: User): Action[] {
  switch (user.status) {
    case UserResponseStatus.Active:
      return ["rename", "suspend", "delete"];
    case UserResponseStatus.Suspended:
      return ["rename", "reactivate", "delete"];
    default:
      return [];
  }
}

/** After a command the screen reads again (F47): the user, and every list of users, its pages of Load more too (F9). */
export const refreshUser = (queryClient: QueryClient, id: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getGetUserQueryKey(id) }),
    queryClient.invalidateQueries({ queryKey: getListUsersQueryKey() }),
    queryClient.invalidateQueries({ queryKey: getListUsersInfiniteQueryKey() }),
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
  if (kind === "sign-in")
    window.location.assign(signInAgainUrl(window.location));
  if (kind === "account-suspended" || kind === "account-deleted") {
    void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
    return;
  }
  if (error.problem && changedMeanwhile.has(error.problem.code))
    void refreshUser(queryClient, id);
}
