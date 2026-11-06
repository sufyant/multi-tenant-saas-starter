"use client";

import { useClerk, useUser } from "@clerk/nextjs";
import { AuthHeading, AuthScreen } from "@repo/ui/components/auth-layout";
import { TextButton } from "@repo/ui/components/sign-in";
import { buttonVariants } from "@repo/ui/components/ui/button";
import { useTranslations } from "next-intl";

/**
 * "You don't have access to the platform" (platform frame 1.08): a signed-in person who is no system admin, or not a user at all
 * (GetMe 404). It shows who is signed in, from Clerk, because such a person has no user of ours. The web app is the way on, and
 * another account the way out.
 */
export function NoAccess({ webAppUrl }: { webAppUrl?: string }) {
  const t = useTranslations();
  const { user } = useUser();
  const { signOut } = useClerk();
  return (
    <AuthScreen brand={t("meta.title")} platformLabel={t("shell.platform")}>
      <AuthHeading
        title={t("access.noAccessTitle")}
        text={t.rich("access.noAccessText", {
          email: () => (
            <bdi dir="ltr">{user?.primaryEmailAddress?.emailAddress}</bdi>
          ),
        })}
      />
      {webAppUrl && (
        <a
          href={webAppUrl}
          className={buttonVariants({
            variant: "outline",
            className: "w-full",
          })}
        >
          {t("access.openWebApp")}
        </a>
      )}
      <p className="text-center text-small font-medium">
        <TextButton
          className="text-foreground"
          onClick={() => void signOut({ redirectUrl: "/sign-in" })}
        >
          {t("access.anotherAccount")}
        </TextButton>
      </p>
    </AuthScreen>
  );
}
