import {
  actionFor,
  getGetMeQueryKey,
  ProblemDetailsError,
} from "@repo/api-client";
import type { QueryClient } from "@tanstack/react-query";

/**
 * What any refused command means beyond its own screen (F4, F47): a 401 goes to sign-in; a suspended or deleted user reads GetMe
 * again, and the shell shows the account's screen. It returns the API's code, or null for an answer that is no Problem Details.
 */
export function afterAccountRefusal(
  queryClient: QueryClient,
  error: unknown,
): string | null {
  if (!(error instanceof ProblemDetailsError)) return null;
  const { kind } = actionFor(error.status, error.problem);
  if (kind === "sign-in") window.location.assign("/sign-in");
  if (kind === "account-suspended" || kind === "account-deleted")
    void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
  return error.problem?.code ?? null;
}
