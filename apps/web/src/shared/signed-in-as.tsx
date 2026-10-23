"use client";

import { TextButton } from "@repo/ui/components/sign-in";
import { useTranslations } from "next-intl";

/** The end of the header around sign-in (the design system's boards): who is signed in, and Sign out. */
export function SignedInAs({
  email,
  onSignOut,
}: {
  email: string;
  onSignOut: () => void;
}) {
  const t = useTranslations();
  return (
    <>
      <span>
        {t.rich("signIn.signedInAs", {
          email: () => <bdi dir="ltr">{email}</bdi>,
        })}
      </span>
      <TextButton onClick={onSignOut}>{t("common.signOut")}</TextButton>
    </>
  );
}
