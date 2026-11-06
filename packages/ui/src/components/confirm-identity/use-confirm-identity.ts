"use client";

import type { useSession } from "@clerk/nextjs";
import { useEffect, useRef, useState } from "react";
import {
  type SecondFactor,
  type SignInErrorKey,
  type SignInStep,
  signInErrorKey,
} from "../sign-in";

export type VerifyingSession = NonNullable<
  ReturnType<typeof useSession>["session"]
>;

/** The check Clerk asks for: the email code, the authenticator, or both one after the other. */
export type VerificationLevel =
  | "first_factor"
  | "second_factor"
  | "multi_factor";

/** Where the check stands: starting, waiting for the email code, or waiting for the authenticator or a backup code. */
export type ConfirmStep = "starting" | "email" | "second";

const strategies = { totp: "totp", backupCode: "backup_code" } as const;

/**
 * Clerk's session reverification (F42, F62), the one copy of its calls. It starts the check at the level asked, once; the email
 * code goes to the person's address, and the authenticator's code or a backup code follows when the level needs a second
 * factor. After a check that completes it asks Clerk for a fresh session token, which writes the session cookie again, so the
 * proxy and the BFF read the new factor's age; then it calls `onVerified`. `ConfirmIdentity` and the platform's two-step
 * challenge draw it.
 */
export function useConfirmIdentity({
  session,
  level,
  onVerified,
  onUnexpectedError,
}: {
  session: VerifyingSession;
  level: VerificationLevel;
  onVerified: () => void;
  /** The app's error tracking, for a Clerk error that has no message yet. */
  onUnexpectedError?: (error: Error) => void;
}) {
  const [step, setStep] = useState<ConfirmStep>("starting");
  const [email, setEmail] = useState<{ id: string; address: string }>();
  const [error, setError] = useState<SignInErrorKey>();
  const [pending, setPending] = useState(false);
  const [resent, setResent] = useState(false);
  const started = useRef(false);

  const fail = (reason: unknown, at: SignInStep) =>
    setError(signInErrorKey(reason, at, onUnexpectedError));

  const sendEmailCode = (emailAddressId: string) =>
    session.prepareFirstFactorVerification({
      strategy: "email_code",
      emailAddressId,
    });

  // Clerk answers where the check stands: the next step, done, or a state no step serves.
  const follow = async (status: string) => {
    if (status === "complete") {
      await session.getToken({ skipCache: true });
      return onVerified();
    }
    if (status === "needs_second_factor") return setStep("second");
    if (status === "needs_first_factor") return setStep("email");
    setError("generic");
  };

  // The check starts once, after the first render: the ref keeps a later render, or Strict Mode's second mount, from sending a
  // second code.
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    void (async () => {
      try {
        const verification = await session.startVerification({ level });
        const factor = verification.supportedFirstFactors?.find(
          (each) => each.strategy === "email_code",
        );
        if (verification.status === "needs_first_factor" && factor) {
          setEmail({
            id: factor.emailAddressId,
            address: factor.safeIdentifier,
          });
          await sendEmailCode(factor.emailAddressId);
        }
        await follow(verification.status);
      } catch (reason) {
        fail(reason, level === "second_factor" ? "totp" : "code");
      }
    })();
  });

  /** Checks a code: the email's in the email step, else the authenticator's or a backup code. */
  const verify = async (code: string, factor: SecondFactor = "totp") => {
    setPending(true);
    setError(undefined);
    try {
      const verification =
        step === "email"
          ? await session.attemptFirstFactorVerification({
              strategy: "email_code",
              code,
            })
          : await session.attemptSecondFactorVerification({
              strategy: strategies[factor],
              code,
            });
      await follow(verification.status);
    } catch (reason) {
      fail(reason, step === "email" ? "code" : factor);
    }
    setPending(false);
  };

  const resend = async () => {
    if (!email) return;
    setError(undefined);
    try {
      await sendEmailCode(email.id);
      setResent(true);
    } catch (reason) {
      fail(reason, "code");
    }
  };

  return {
    step,
    email: email?.address,
    verify,
    resend,
    resent,
    error,
    pending,
    clearError: () => setError(undefined),
  };
}
