"use client";

import { useClerk, useSession, useUser } from "@clerk/nextjs";
import { AuthLayout } from "@repo/ui/components/auth-layout";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { hasVerifiedSecondFactor } from "@/shared/second-factor";
import { SignedInAs } from "@/shared/signed-in-as";
import { BackupCodes } from "./backup-codes";
import { TotpChallenge } from "./totp-challenge";
import { TotpEnrol } from "./totp-enrol";

type Phase = "enrol" | "backupCodes" | "challenge";

/**
 * /two-step (F42): the platform's door needs a second factor (A6), so a session without one comes here before anything else. No
 * authenticator app yet: the enrolment, then the backup codes. An app but no verified second factor in this session: the
 * challenge. A session that has one goes on to the platform.
 */
export function TwoStepPage() {
  const t = useTranslations();
  const { isLoaded, user } = useUser();
  const { session } = useSession();
  const { signOut } = useClerk();
  const [phase, setPhase] = useState<Phase>();
  const [codes, setCodes] = useState<string[]>([]);

  const go = () => window.location.assign("/");
  // The proxy reads the session's token, so the page waits for a token that holds the new factor before it leaves. After the
  // challenge, `useReverification` has asked for it already.
  const enter = async () => {
    await session?.getToken({ skipCache: true });
    go();
  };

  const verified = hasVerifiedSecondFactor(session?.factorVerificationAge);
  // biome-ignore lint/correctness/useExhaustiveDependencies: enter reads only the session, which the effect already follows.
  useEffect(() => {
    if (phase === undefined && verified) void enter();
  }, [phase, verified]);

  // After the enrolment: a session that Clerk did not mark with the second factor passes the challenge too (step 21b, gate C).
  const afterBackupCodes = async () => {
    const fresh = await session?.reload();
    if (hasVerifiedSecondFactor(fresh?.factorVerificationAge)) return enter();
    setPhase("challenge");
  };

  const leave = () => void signOut({ redirectUrl: "/sign-in" });

  if (!isLoaded || !user || !session || (phase === undefined && verified))
    return <Skeleton label={t("common.loading")} />;
  const current = phase ?? (user.totpEnabled ? "challenge" : "enrol");

  return (
    <AuthLayout
      brand={t("meta.title")}
      platformLabel={t("shell.platform")}
      account={
        <SignedInAs
          email={user.primaryEmailAddress?.emailAddress ?? ""}
          onSignOut={leave}
        />
      }
    >
      {current === "enrol" && (
        <TotpEnrol
          user={user}
          onEnrolled={(backupCodes) => {
            setCodes(backupCodes);
            setPhase("backupCodes");
          }}
        />
      )}
      {current === "backupCodes" && (
        <BackupCodes codes={codes} onContinue={() => void afterBackupCodes()} />
      )}
      {current === "challenge" && (
        <TotpChallenge session={session} onVerified={go} onSignOut={leave} />
      )}
    </AuthLayout>
  );
}
