import { ProblemDetailsError } from "./mutator";

/**
 * The queries' retry rule of every app (F8): up to three retries for a network error or a 5xx. A 4xx is the API's answer to this
 * request, and the same request gets it again (TanStack Query, "Query Retries").
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
