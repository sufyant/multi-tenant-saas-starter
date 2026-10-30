"use client";

import { useClerk, useUser } from "@clerk/nextjs";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { useTranslations } from "next-intl";
import { PersonalBell } from "./notification-bell";

/**
 * "No access yet": the same text for a person who is not a user yet (GetMe 404), a user with no organisation and a slug that is
 * not in the user's list. It never says whether an invitation exists. It shows who is signed in, from Clerk, and Sign out (the
 * NoAccess board). The personal bell shows a removal from an organisation (the NoAccessBell board).
 */
export function NoAccess() {
  const t = useTranslations();
  const { user } = useUser();
  const { signOut } = useClerk();
  const email = user?.primaryEmailAddress?.emailAddress;
  return (
    <div className="flex min-h-dvh flex-col">
      <header className="flex justify-end px-4 pt-4 tablet:px-6 tablet:pt-5">
        <PersonalBell />
      </header>
      <main className="mx-auto flex w-full max-w-xl flex-1 items-center p-6">
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
    </div>
  );
}
