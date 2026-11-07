"use client";

import { Button } from "@repo/ui/components/button";
import {
  useConfirmIdentity,
  type VerifyingSession,
} from "@repo/ui/components/confirm-identity";
import { SecondFactorStep } from "@repo/ui/components/sign-in";
import { useTranslations } from "next-intl";
import { reportError } from "@/error-tracking/report-error";

/**
 * The second factor of a session that has none verified (platform frames 1.05 and 1.06), through Clerk's session reverification at
 * the level `second_factor` (`useConfirmIdentity` of `packages/ui`). It serves a returning system admin whose session lost the
 * factor, and the one who just enrolled.
 */
export function TotpChallenge({
  session,
  onVerified,
  onSignOut,
}: {
  session: VerifyingSession;
  onVerified: () => void;
  onSignOut: () => void;
}) {
  const t = useTranslations();
  const { verify, error, pending, clearError } = useConfirmIdentity({
    session,
    level: "second_factor",
    onVerified,
    onUnexpectedError: reportError,
  });

  return (
    <SecondFactorStep
      title={t("signInPage.secondFactorTitle")}
      text={t("signInPage.secondFactorText")}
      onSubmit={(code, factor) => void verify(code, factor)}
      onFactorChange={clearError}
      exit={
        <Button variant="ghost" onClick={onSignOut}>
          {t("common.signOut")}
        </Button>
      }
      pending={pending}
      error={error}
    />
  );
}
