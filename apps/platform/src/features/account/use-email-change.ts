"use client";

import {
  actionFor,
  changeEmail,
  getGetMeQueryKey,
  ProblemDetailsError,
  problemOf,
  requestEmailChange,
  savedUnderKey,
} from "@repo/api-client";
import type { EmailChangeProps } from "@repo/ui/components/account-security";
import { useMutation, useQueryClient } from "@tanstack/react-query";

/**
 * The calls of the email change (F62): `RequestEmailChange` with the intent's Idempotency-Key (F48) and `ChangeEmail` with the
 * code, through `useMutation` (F2), and how a failure reads. After the change every screen reads GetMe again (F47).
 */
export function useEmailChange(): EmailChangeProps {
  const queryClient = useQueryClient();
  const request = useMutation({
    mutationFn: ({ email, key }: { email: string; key: string }) =>
      requestEmailChange({ email }, { "Idempotency-Key": key }),
  });
  const confirm = useMutation({
    mutationFn: (code: string) => changeEmail({ code }),
  });
  return {
    send: (email, key) => request.mutateAsync({ email, key }),
    verify: (code) => confirm.mutateAsync(code),
    readFailure: (error) => {
      const action =
        error instanceof ProblemDetailsError
          ? actionFor(error.status, error.problem)
          : null;
      return {
        problem: problemOf(error),
        fields: action?.kind === "field-errors" ? action.fields : null,
        saved: savedUnderKey(error),
      };
    },
    onChanged: () =>
      void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() }),
  };
}
