"use client";

import { SecondFactorStep, TextButton } from "@repo/ui/components/sign-in";
import { useTranslations } from "next-intl";
import { type Session, useReverification } from "@/shared/use-reverification";

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
  const { verify, error, pending, clearError } = useReverification(
    session,
    onVerified,
  );

  return (
    <SecondFactorStep
      onSubmit={(code, factor) => void verify(code, factor)}
      onFactorChange={clearError}
      exit={<TextButton onClick={onSignOut}>{t("signOut")}</TextButton>}
      pending={pending}
      error={error}
    />
  );
}
