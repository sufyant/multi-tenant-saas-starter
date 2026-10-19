"use client";

import type { useSession } from "@clerk/nextjs";
import {
  type SecondFactor,
  SecondFactorStep,
  type SignInErrorKey,
  signInErrorKey,
  TextButton,
} from "@repo/ui/components/sign-in";
import { useTranslations } from "next-intl";
import { useEffect, useRef, useState } from "react";

type Session = NonNullable<ReturnType<typeof useSession>["session"]>;

const strategies = { totp: "totp", backupCode: "backup_code" } as const;

/**
 * The second factor of a session that has none verified (the TotpChallenge board), through Clerk's session reverification at
 * the level `second_factor`. It serves a returning system admin whose session lost the factor, and the one who just enrolled.
 */
export function TotpChallenge({
  session,
  onVerified,
  onSignOut,
}: {
  session: Session;
  onVerified: () => void;
  onSignOut: () => void;
}) {
  const t = useTranslations("common");
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
      if (verification.status === "complete") return onVerified();
      setError("generic");
    } catch (reason) {
      setError(signInErrorKey(reason, factor));
    }
    setPending(false);
  };

  return (
    <SecondFactorStep
      onSubmit={(code, factor) => void verify(code, factor)}
      onFactorChange={() => setError(undefined)}
      exit={<TextButton onClick={onSignOut}>{t("signOut")}</TextButton>}
      pending={pending}
      error={error}
    />
  );
}
