"use client";

import { useClerk, useUser } from "@clerk/nextjs";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { useTranslations } from "next-intl";

/**
 * "No access yet": the same text for a person who is not a user yet (GetMe 404), a user with no organisation and a slug that is
 * not in the user's list. It never says whether an invitation exists. It shows who is signed in, from Clerk, and Sign out (the
 * NoAccess board).
 */
export function NoAccess() {
  const t = useTranslations();
  const { user } = useUser();
  const { signOut } = useClerk();
  const email = user?.primaryEmailAddress?.emailAddress;
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl items-center p-6">
      <EmptyState
        title={t("problems.noAccessTitle")}
        description={
          <>
            {t("problems.noAccessText")}
            {email && (
              <span className="mt-2 block">
                {t.rich("signIn.signedInAs", {
                  email: () => <bdi dir="ltr">{email}</bdi>,
                })}
              </span>
            )}
          </>
        }
        action={
          <Button
            variant="primary"
            onClick={() => void signOut({ redirectUrl: "/sign-in" })}
          >
            {t("common.signOut")}
          </Button>
        }
      />
    </main>
  );
}
