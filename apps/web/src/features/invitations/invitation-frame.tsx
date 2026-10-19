"use client";

import { useClerk, useUser } from "@clerk/nextjs";
import { AuthLayout } from "@repo/ui/components/auth-layout";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { SignedInAs } from "@/shared/signed-in-as";

/** The frame of the accept screens: the product, who is signed in and Sign out. A person who is not a user yet has only Clerk's address. */
export function InvitationFrame({ children }: { children: ReactNode }) {
  const t = useTranslations();
  const { user } = useUser();
  const { signOut } = useClerk();
  const email = user?.primaryEmailAddress?.emailAddress;
  return (
    <AuthLayout
      brand={t("meta.title")}
      account={
        email && (
          <SignedInAs
            email={email}
            onSignOut={() => void signOut({ redirectUrl: "/sign-in" })}
          />
        )
      }
    >
      {children}
    </AuthLayout>
  );
}
