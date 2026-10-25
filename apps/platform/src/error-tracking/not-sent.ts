import { ProblemDetailsError } from "@repo/api-client";
import { isExpectedClerkError } from "@repo/ui/components/sign-in";
import { isCancelledError } from "@tanstack/react-query";

// Next.js's control flow: redirect() and notFound(), forbidden() and unauthorized(). They are thrown on purpose and carry these
// digests.
const controlFlowDigests = ["NEXT_REDIRECT", "NEXT_HTTP_ERROR_FALLBACK;"];

const digestOf = (error: unknown) =>
  typeof error === "object" &&
  error !== null &&
  "digest" in error &&
  typeof error.digest === "string"
    ? error.digest
    : undefined;

/**
 * Whether an error stays out of error tracking (F55). The one place of the rule:
 * - an answer of the API: a 4xx is a message on screen, and the API sends its own 5xx;
 * - a Clerk error that the sign-in screens show as a message;
 * - a request or a navigation that was cancelled;
 * - Next.js's control flow.
 */
export function isNotSent(error: unknown): boolean {
  if (error instanceof ProblemDetailsError) return true;
  if (isExpectedClerkError(error)) return true;
  if (isCancelledError(error)) return true;
  if (
    typeof error === "object" &&
    error !== null &&
    "name" in error &&
    error.name === "AbortError"
  )
    return true;
  const digest = digestOf(error);
  return (
    digest !== undefined &&
    controlFlowDigests.some((prefix) => digest.startsWith(prefix))
  );
}
