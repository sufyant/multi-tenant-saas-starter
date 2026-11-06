"use client";

import {
  actionFor,
  changeEmail,
  getGetMeQueryKey,
  ProblemDetailsError,
  problemOf,
  requestEmailChange,
  savedUnderKey,
  schemas,
} from "@repo/api-client";
import type { EmailChangeProps } from "@repo/ui/components/account-security";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";

// The generated body of RequestEmailChange (F11); the API trims the address and needs one, so the form does too.
const schema = schemas.RequestEmailChangeBody.extend({
  email: z
    .string()
    .trim()
    .min(1)
    .max(schemas.requestEmailChangeBodyEmailMax)
    .pipe(z.email()),
});

/**
 * The calls of the email change (F62): `RequestEmailChange` with the intent's Idempotency-Key (F48) and `ChangeEmail` with the
 * code, through `useMutation` (F2), and how a failure reads: a 401 goes to sign-in, a suspended or deleted account reads GetMe
 * again, so the shell shows its page (F4, F47). After the change every screen reads GetMe again.
 */
export function useEmailChange(): EmailChangeProps {
  const queryClient = useQueryClient();
  const readMeAgain = () =>
    void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
  const request = useMutation({
    mutationFn: ({ email, key }: { email: string; key: string }) =>
      requestEmailChange({ email }, { "Idempotency-Key": key }),
  });
  const confirm = useMutation({
    mutationFn: (code: string) => changeEmail({ code }),
  });
  return {
    schema,
    send: (email, key) => request.mutateAsync({ email, key }),
    verify: (code) => confirm.mutateAsync(code),
    readFailure: (error) => {
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
        readMeAgain();
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
    onChanged: readMeAgain,
  };
}
