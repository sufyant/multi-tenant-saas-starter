import type { ProblemDetails } from "./generated/endpoints.schemas.gen";
import { ProblemDetailsError } from "./mutator";

/** A failed call's Problem Details, with the reference that support searches for. */
export type FailedCall = ProblemDetails & { reference: string };

/**
 * The code, the trace id and the reference of a failed call, or null when the answer was no Problem Details (a dropped connection).
 * `packages/ui` takes it as it is: its list error and failure text show the reference without reading the client.
 */
export function problemOf(error: unknown): FailedCall | null {
  if (!(error instanceof ProblemDetailsError) || !error.problem) return null;
  return { ...error.problem, reference: referenceOf(error.problem.traceId) };
}

/** The part of a W3C trace id that support searches for: its 32 hex digits, else the whole id. */
export function referenceOf(traceId: string): string {
  return /^[\da-f]{2}-([\da-f]{32})-/.exec(traceId)?.[1] ?? traceId;
}
