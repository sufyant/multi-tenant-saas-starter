"use client";

import { AuthCard, AuthLayout } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { TermsAuthFooter } from "@/shared/terms-link";

/** The key of this tab's storage that carries the deleted account's address to its page, once. */
export const deletedEmailKey = "deleted-account-email";

/**
 * "Your account was deleted" (6.24, F62): the page after the deletion, signed out, in the frame before sign-in with its footer
 * (F59). It names the address the confirmation went to, which Delete my account left in this tab's storage, so the address bar
 * never holds it (the privacy rules); it reads it once and removes it.
 */
export function DeletedAccount() {
  const t = useTranslations();
  const [email, setEmail] = useState<string | null>(null);
  useEffect(() => {
    setEmail(sessionStorage.getItem(deletedEmailKey));
    sessionStorage.removeItem(deletedEmailKey);
  }, []);
  return (
    <AuthLayout brand={t("meta.title")} footer={<TermsAuthFooter />}>
      <AuthCard>
        <h1 className="text-page-title">{t("securityPage.deletedTitle")}</h1>
        <p className="text-body text-muted-foreground">
          {email
            ? t.rich("securityPage.deletedText", {
                email: () => <bdi dir="ltr">{email}</bdi>,
              })
            : t("securityPage.deletedTextNoAddress")}
        </p>
        <Button
          variant="primary"
          render={<Link href="/sign-in" />}
          nativeButton={false}
          className="self-start"
        >
          {t("securityPage.goToSignIn")}
        </Button>
      </AuthCard>
    </AuthLayout>
  );
}
