"use client";

import { AuthCard } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import Link from "next/link";
import { useTranslations } from "next-intl";

/**
 * 403 `invitation.email_mismatch` (the AcceptWrongEmail board): the caller's own address and nothing about the invitation,
 * whatever its state. Switch account signs out and comes back to this page through sign-in.
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
    <AuthCard>
      <h1 className="text-page-title">{t("wrongEmailTitle")}</h1>
      <p className="text-body text-muted-foreground">
        {t.rich("wrongEmailText", {
          email: () => <bdi dir="ltr">{email}</bdi>,
        })}
      </p>
      <div className="flex flex-wrap gap-2">
        <Button variant="primary" onClick={onSwitchAccount}>
          {t("switchAccount")}
        </Button>
        <Button render={<Link href="/" />} nativeButton={false}>
          {t("backHome")}
        </Button>
      </div>
    </AuthCard>
  );
}
