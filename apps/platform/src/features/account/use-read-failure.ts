"use client";

import {
  actionFor,
  getGetMeQueryKey,
  ProblemDetailsError,
  problemOf,
  savedUnderKey,
} from "@repo/api-client";
import type { ReadFailure } from "@repo/ui/components/account-security";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";

/**
 * How a failure of a personal call reads (F62): a 401 goes to sign-in, a suspended or deleted account reads GetMe again, so the
 * shell shows its page (F4, F47); the rest is the card's to say. The function stays the same across renders, so an effect can
 * depend on it.
 */
export function useReadFailure(): ReadFailure {
  const queryClient = useQueryClient();
  return useCallback<ReadFailure>(
    (error) => {
      const action =
        error instanceof ProblemDetailsError
          ? actionFor(error.status, error.problem)
          : null;
      if (action?.kind === "sign-in")
        window.location.assign(signInAgainUrl(window.location));
      if (
        action?.kind === "account-suspended" ||
        action?.kind === "account-deleted"
      )
        void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
      return {
        problem: problemOf(error),
        fields: action?.kind === "field-errors" ? action.fields : null,
        saved: savedUnderKey(error),
        handled:
          action?.kind === "sign-in" ||
          action?.kind === "account-suspended" ||
          action?.kind === "account-deleted",
      };
    },
    [queryClient],
  );
}
