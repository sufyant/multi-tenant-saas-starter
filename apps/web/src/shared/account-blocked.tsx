"use client";

import { useClerk, useUser } from "@clerk/nextjs";
import { ErrorCode } from "@repo/api-client";
import { AuthCard, AuthLayout } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { Status } from "@repo/ui/components/status";
import { useTranslations } from "next-intl";

/** The code of the API's refusal, which names the page's texts: `account.user.suspended.title`. */
export type AccountBlock =
  | typeof ErrorCode.UserSuspended
  | typeof ErrorCode.UserDeleted;

/**
 * The page of a user the API refuses on every request (the AccountBlocked board): 403 `user.suspended` or `user.deleted` (A9). It
 * shows who is signed in, from Clerk, and the one way out: Sign out, or Use another email, which also signs out.
 */
export function AccountBlocked({ block }: { block: AccountBlock }) {
  const t = useTranslations();
  const { user } = useUser();
  const { signOut } = useClerk();
  const email = user?.primaryEmailAddress?.emailAddress;
  return (
    <AuthLayout brand={t("meta.title")}>
      <AuthCard>
        <Status
          tone={block === ErrorCode.UserSuspended ? "warning" : "neutral"}
        >
          {t(`account.${block}.status`)}
        </Status>
        <h1 className="text-page-title">{t(`account.${block}.title`)}</h1>
        <p className="text-body text-muted-foreground">
          {t(`account.${block}.text`)}
        </p>
        {email && (
          <p className="text-small text-muted-foreground">
            {t.rich("signIn.signedInAs", {
              email: () => (
                <bdi dir="ltr" className="font-medium text-foreground">
                  {email}
                </bdi>
              ),
            })}
          </p>
        )}
        <div>
          <Button
            variant="primary"
            onClick={() => void signOut({ redirectUrl: "/sign-in" })}
          >
            {t(`account.${block}.action`)}
          </Button>
        </div>
      </AuthCard>
    </AuthLayout>
  );
}
