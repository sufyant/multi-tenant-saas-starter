import {
  actionFor,
  ErrorCode,
  getGetMeQueryKey,
  getGetUserQueryKey,
  getListUsersQueryKey,
  ProblemDetailsError,
  problemOf,
  type UserResponse,
} from "@repo/api-client";
import type { QueryClient } from "@tanstack/react-query";
import { nameOf } from "@/shared/name-of";

/** The name of a system admin, or their email: a root admin whom the door added has no names yet (A7). */
export const adminNameOf = (
  user: Pick<UserResponse, "firstName" | "lastName" | "email">,
) => nameOf(user) ?? user.email ?? "";

/** The API asks for a fresh second factor before a grant or a revoke (A6a): the dialog asks for the code, then sends again. */
export const needsReverification = (error: unknown) =>
  problemOf(error)?.code === ErrorCode.SystemAdminReverificationRequired;

/** After a change the screen reads again (F47): the user, and every page of both lists, the System admins and the Users. */
export const refreshSystemAdmin = (queryClient: QueryClient, id: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getGetUserQueryKey(id) }),
    queryClient.invalidateQueries({ queryKey: getListUsersQueryKey() }),
  ]);

/** A refusal that says the user or the grant changed meanwhile: the dialog shows its text, and the screen reads again (F47). */
const changedMeanwhile = new Set<string>([
  ErrorCode.SystemAdminExists,
  ErrorCode.SystemAdminNotFound,
  ErrorCode.SystemAdminRoot,
  ErrorCode.SystemAdminCannotRevokeSelf,
  ErrorCode.UserNotFound,
  ErrorCode.UserDeleted,
  ErrorCode.UserSuspended,
]);

/**
 * What a refused grant or revoke means beyond its dialog (F4, F47): a 401 goes to sign-in; the caller's own suspension or deletion
 * (403) reads GetMe again, and the shell shows the account's screen; a conflict reads the lists and the user again. A 409
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
    void refreshSystemAdmin(queryClient, id);
}
