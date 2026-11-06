"use client";

import { AuthCard } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";

/** The key of this tab's storage that carries the deleted account's address to its page, once. */
export const deletedEmailKey = "deleted-account-email";

/**
 * "Your account was deleted" (6.24, F62): the page after the deletion, signed out and outside the shell. It names the address the
 * confirmation went to, which Delete my account left in this tab's storage, so the address bar never holds it (the privacy rules);
 * it reads it once and removes it.
 */
export function DeletedAccount() {
  const t = useTranslations("securityPage");
  const [email, setEmail] = useState<string | null>(null);
  useEffect(() => {
    setEmail(sessionStorage.getItem(deletedEmailKey));
    sessionStorage.removeItem(deletedEmailKey);
  }, []);
  return (
    <main className="flex min-h-dvh items-center justify-center bg-background p-4 text-foreground">
      <div className="w-full max-w-[420px]">
        <AuthCard>
          <h1 className="text-page-title">{t("deletedTitle")}</h1>
          <p className="text-body text-muted-foreground">
            {email
              ? t.rich("deletedText", {
                  email: () => <bdi dir="ltr">{email}</bdi>,
                })
              : t("deletedTextNoAddress")}
          </p>
          <Button
            variant="primary"
            render={<Link href="/sign-in" />}
            nativeButton={false}
          >
            {t("goToSignIn")}
          </Button>
        </AuthCard>
      </div>
    </main>
  );
}
