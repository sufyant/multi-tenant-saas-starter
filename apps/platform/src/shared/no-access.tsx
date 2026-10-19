"use client";

import { useClerk, useUser } from "@clerk/nextjs";
import { AuthCard, AuthLayout } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { useTranslations } from "next-intl";

/**
 * "You don't have access" (the NoAccess board): a signed-in person who is no system admin, or not a user at all (GetMe 404). It
 * shows who is signed in, from Clerk, because such a person has no user of ours.
 */
export function NoAccess() {
  const t = useTranslations();
  const { user } = useUser();
  const { signOut } = useClerk();
  return (
    <AuthLayout brand={t("meta.title")} platformLabel={t("shell.platform")}>
      <AuthCard>
        <h1 className="text-page-title">{t("access.noAccessTitle")}</h1>
        <p className="text-body text-muted-foreground">
          {t.rich("access.noAccessText", {
            email: () => (
              <bdi dir="ltr">{user?.primaryEmailAddress?.emailAddress}</bdi>
            ),
          })}
        </p>
        <div>
          <Button
            variant="primary"
            onClick={() => void signOut({ redirectUrl: "/sign-in" })}
          >
            {t("common.signOut")}
          </Button>
        </div>
      </AuthCard>
    </AuthLayout>
  );
}
