import { ProblemDetailsError } from "@repo/api-client";

/** The code and the trace id of a failed call, or null when the answer was no Problem Details (a dropped connection). */
export function problemOf(error: unknown) {
  return error instanceof ProblemDetailsError ? error.problem : null;
}

/** The part of a W3C trace id that support searches for: its 32 hex digits, else the whole id. */
export function referenceOf(traceId: string): string {
  return /^[\da-f]{2}-([\da-f]{32})-/.exec(traceId)?.[1] ?? traceId;
}
