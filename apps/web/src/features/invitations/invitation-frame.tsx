"use client";

import { useClerk, useUser } from "@clerk/nextjs";
import { brand } from "@repo/brand";
import { AuthFooter, AuthScreen } from "@repo/ui/components/auth-layout";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { setLocale } from "@/i18n/set-locale";
import { SignedInAs } from "@/shared/signed-in-as";

/**
 * The frame of the accept screens (the Entry frames): the card with the brand's mark. `footer` adds the language and Privacy
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
      footer={
        footer && (
          <AuthFooter
            onLanguageChange={setLocale}
            privacyUrl={brand.privacyUrl}
          />
        )
      }
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
