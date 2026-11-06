"use client";

import {
  changeEmail,
  getGetMeQueryKey,
  requestEmailChange,
  schemas,
} from "@repo/api-client";
import type { EmailChangeProps } from "@repo/ui/components/account-security";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { useReadFailure } from "./use-read-failure";

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
 * code, through `useMutation` (F2); a failure reads as `useReadFailure` says. After the change every screen reads GetMe again.
 */
export function useEmailChange(): EmailChangeProps {
  const queryClient = useQueryClient();
  const readFailure = useReadFailure();
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
    readFailure,
    onChanged: () =>
      void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() }),
  };
}
