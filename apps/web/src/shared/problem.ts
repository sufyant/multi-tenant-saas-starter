import { actionFor, ErrorCode, ProblemDetailsError } from "@repo/api-client";
import type { AccountBlock } from "./account-blocked";

/**
 * Whether the API refuses the user on every request (A9): 403 `user.suspended` or `user.deleted` (F4). These are expected answers,
 * so no screen shows a toast or logs them.
 */
export function accountBlockOf(error: unknown): AccountBlock | null {
  if (!(error instanceof ProblemDetailsError)) return null;
  const { kind } = actionFor(error.status, error.problem);
  if (kind === "account-suspended") return ErrorCode.UserSuspended;
  if (kind === "account-deleted") return ErrorCode.UserDeleted;
  return null;
}
