"use client";

import { useClerk, useUser } from "@clerk/nextjs";
import { AuthScreen } from "@repo/ui/components/auth-layout";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { SignedInAs } from "@/shared/signed-in-as";
import { TermsAuthFooter } from "@/shared/terms-link";

/**
 * The frame of the accept screens (the Entry frames): the card with the brand's mark. `footer` adds the language, Terms and Privacy
 * (1.13, 1.16); `account` names who is signed in under the card, with Sign out. A person who is not a user yet has only Clerk's
 * address.
 */
export function InvitationFrame({
  footer,
  account,
  children,
}: {
  footer?: boolean;
  account?: boolean;
  children: ReactNode;
}) {
  const t = useTranslations();
  const { user } = useUser();
  const { signOut } = useClerk();
  const email = user?.primaryEmailAddress?.emailAddress;
  return (
    <AuthScreen
      brand={t("meta.title")}
      footer={footer && <TermsAuthFooter />}
      below={
        account &&
        email && (
          <SignedInAs
            email={email}
            onSignOut={() => void signOut({ redirectUrl: "/sign-in" })}
          />
        )
      }
    >
      {children}
    </AuthScreen>
  );
}
