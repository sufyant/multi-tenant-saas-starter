"use client";

import { AuthHeading } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import Link from "next/link";
import { useTranslations } from "next-intl";

/**
 * 403 `invitation.email_mismatch` (frame 1.17): the caller's own address and nothing about the invitation, whatever its state; the
 * refusal holds no invited address (step 20l). Sign out and continue comes back to this page, which signs the person in.
 */
export function WrongEmail({
  email,
  onSwitchAccount,
}: {
  email: string;
  onSwitchAccount: () => void;
}) {
  const t = useTranslations("invitations");
  return (
    <>
      <AuthHeading
        title={t("wrongEmailTitle")}
        text={t.rich("wrongEmailText", {
          email: () => <bdi dir="ltr">{email}</bdi>,
        })}
      />
      <Button variant="primary" className="w-full" onClick={onSwitchAccount}>
        {t("signOutAndContinue")}
      </Button>
      <Button
        variant="ghost"
        className="self-center"
        render={<Link href="/" />}
        nativeButton={false}
      >
        {t("goBack")}
      </Button>
    </>
  );
}
