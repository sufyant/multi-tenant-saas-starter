"use client";

import { useClerk, useUser } from "@clerk/nextjs";
import { getGetMeQueryOptions } from "@repo/api-client";
import { AccountMenu } from "@repo/ui/components/account-menu";
import { useAppFrame } from "@repo/ui/components/app-frame";
import { NoticeScreen } from "@repo/ui/components/auth-layout";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { PersonalBell } from "./notification-bell";
import { personalPages } from "./personal-pages";

/**
 * "No access yet" (frame 1.19): the same text for a person who is not a user yet (GetMe 404), a user with no organisation and a
 * slug that is not in the user's list. It never says whether an invitation exists. The account button at the top end names the
 * person and holds Profile, which opens without an organisation, and Sign out; a person who is not a user yet has only Clerk's
 * address and no Profile. The personal bell beside it shows a removal from an organisation (F57).
 */
export function NoAccess() {
  const t = useTranslations();
  const { user } = useUser();
  const { signOut } = useClerk();
  const { version } = useAppFrame();
  // The page may show because GetMe failed: reading it again on mount would take the page away.
  const me = useQuery({ ...getGetMeQueryOptions(), retryOnMount: false });
  const email = me.data?.email ?? user?.primaryEmailAddress?.emailAddress ?? "";
  const name = me.data?.name ?? email;
  return (
    <NoticeScreen
      title={t("problems.noAccessTitle")}
      text={t("problems.noAccessText")}
      corner={
        <>
          <PersonalBell />
          {name && (
            <AccountMenu
              name={name}
              email={email}
              person={{ name: me.data?.name ?? null, email }}
              profileHref={me.data && personalPages.profile}
              onSignOut={() => void signOut({ redirectUrl: "/sign-in" })}
              version={version}
              variant="chip"
              linkComponent={Link}
            />
          )}
        </>
      }
    />
  );
}
