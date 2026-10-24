import { actionFor, ErrorCode, ProblemDetailsError } from "@repo/api-client";
import type { AccountBlock } from "./account-blocked";

/** The code and the trace id of a failed call, or null when the answer was no Problem Details (a dropped connection). */
export function problemOf(error: unknown) {
  return error instanceof ProblemDetailsError ? error.problem : null;
}

/** The part of a W3C trace id that support searches for: its 32 hex digits, else the whole id. */
export function referenceOf(traceId: string): string {
  return /^[\da-f]{2}-([\da-f]{32})-/.exec(traceId)?.[1] ?? traceId;
}

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

/**
 * The queries' retry rule (F8): up to three retries for a network error or a 5xx. A 4xx is the API's answer to this request, and
 * the same request gets it again (TanStack Query, "Query Retries").
 */
export function retryUnlessClientError(
  failureCount: number,
  error: unknown,
): boolean {
  const clientError =
    error instanceof ProblemDetailsError &&
    error.status >= 400 &&
    error.status < 500;
  return failureCount < 3 && !clientError;
}
