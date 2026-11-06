"use client";

import type { useUser } from "@clerk/nextjs";
import { useEffect, useRef, useState } from "react";
import { type SignInErrorKey, signInErrorKey } from "../sign-in";

type User = NonNullable<ReturnType<typeof useUser>["user"]>;
export type Totp = Awaited<ReturnType<User["createTOTP"]>>;

/** The typed key in groups of four, as authenticator apps show it: "JBSW Y3DP EHPK". */
export const groupsOfFour = (secret: string) =>
  secret.match(/.{1,4}/g)?.join(" ") ?? "";

/**
 * The enrolment of an authenticator app, the one copy of its Clerk calls (F42, F62). Clerk makes the secret once; the six digits
 * the app shows then turn the factor on, and Clerk answers with the ten backup codes. The platform's two-step gate and the
 * tenant web's Account and security use it. `create` is Clerk's `createTOTP`, or the same call through reverification.
 */
export function useTotpEnrolment({
  user,
  create = () => user.createTOTP(),
  onEnrolled,
  onUnexpectedError,
}: {
  user: User;
  create?: () => Promise<Totp>;
  onEnrolled: (backupCodes: string[]) => void;
  onUnexpectedError?: (error: Error) => void;
}) {
  const [totp, setTotp] = useState<Totp>();
  const [error, setError] = useState<SignInErrorKey>();
  const [pending, setPending] = useState(false);
  // A retry after Clerk turned the factor on but did not make the backup codes asks only for the codes.
  const verified = useRef(false);
  // React runs an effect twice in development; a second createTOTP would replace the secret the QR code shows.
  const started = useRef(false);

  // biome-ignore lint/correctness/useExhaustiveDependencies: the secret is made once; `create` may be a new function each render.
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    create()
      .then(setTotp)
      .catch((reason: unknown) =>
        setError(signInErrorKey(reason, "totp", onUnexpectedError)),
      );
  }, [user]);

  const verify = async (code: string) => {
    setPending(true);
    try {
      // Clerk answers the verification with the ten codes when backup codes are on. Asking for new ones would need a
      // reverification, because the account now has a second factor; so createBackupCode is only the fallback.
      const enrolled = verified.current
        ? undefined
        : await user.verifyTOTP({ code });
      verified.current = true;
      onEnrolled(
        enrolled?.backupCodes ?? (await user.createBackupCode()).codes,
      );
    } catch (reason) {
      setError(signInErrorKey(reason, "totp", onUnexpectedError));
      setPending(false);
    }
  };

  return { totp, verify, error, pending };
}
