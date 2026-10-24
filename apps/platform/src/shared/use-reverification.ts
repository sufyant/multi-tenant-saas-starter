"use client";

import type { useSession } from "@clerk/nextjs";
import {
  type SecondFactor,
  type SignInErrorKey,
  signInErrorKey,
} from "@repo/ui/components/sign-in";
import { useEffect, useRef, useState } from "react";

export type Session = NonNullable<ReturnType<typeof useSession>["session"]>;

const strategies = { totp: "totp", backupCode: "backup_code" } as const;

/**
 * Clerk's session reverification at the level `second_factor` (F42): it starts once, then checks the authenticator's code or a
 * backup code. After a check that completes, it asks Clerk for a fresh session token, which writes the session cookie again, so
 * the proxy and the BFF read the new factor's age (the API reads `fva` from the token, A6a). The two-step challenge and the code
 * step of the system admin dialogs use it.
 */
export function useReverification(session: Session, onVerified: () => void) {
  const [error, setError] = useState<SignInErrorKey>();
  const [pending, setPending] = useState(false);
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    session
      .startVerification({ level: "second_factor" })
      .catch((reason: unknown) => setError(signInErrorKey(reason, "totp")));
  }, [session]);

  const verify = async (code: string, factor: SecondFactor) => {
    setPending(true);
    try {
      const verification = await session.attemptSecondFactorVerification({
        strategy: strategies[factor],
        code,
      });
      if (verification.status === "complete") {
        await session.getToken({ skipCache: true });
        return onVerified();
      }
      setError("generic");
    } catch (reason) {
      setError(signInErrorKey(reason, factor));
    }
    setPending(false);
  };

  return { verify, error, pending, clearError: () => setError(undefined) };
}
