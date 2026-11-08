"use client";

import { useClerk } from "@clerk/nextjs";
import { getGetMeQueryOptions, personalBell } from "@repo/api-client";
import { AccountMenu } from "@repo/ui/components/account-menu";
import { useAppFrame } from "@repo/ui/components/app-frame";
import { AppShell, useTabTitleText } from "@repo/ui/components/app-shell";
import { SettingsIcon } from "@repo/ui/components/icon";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { LocaleSync } from "@/shared/locale-sync";
import { PersonalBell } from "@/shared/notification-bell";
import { myAccountPages, personalPages } from "@/shared/personal-pages";
import { ProblemScreen } from "@/shared/problem-screen";
import { ServerBar } from "@/shared/server-bar";

/**
 * Settings without an organisation (5.11, 6.33, 2.45): Profile and Account and security at `/settings/...`, the platform's paths
 * (decision 69). The settings menu holds "Back to my organisations", which opens the start page, and My account, with the account
 * menu at its foot; the phone's top bar holds the personal bell (F57). A person of any organisation, or of none, opens the same
 * pages. A suspended or deleted account shows its page in place of the shell (F4).
 */
export function PersonalShell({ children }: { children: ReactNode }) {
  const t = useTranslations();
  const router = useRouter();
  const pathname = usePathname();
  const { signOut } = useClerk();
  const { version } = useAppFrame();
  const me = useQuery(getGetMeQueryOptions());
  const unread = useQuery(personalBell().count);
  const tabTitle = useTabTitleText(t("meta.title"));
  if (me.error) return <ProblemScreen error={me.error} />;

  const myAccount = myAccountPages(t);
  const currentItem = myAccount.find((item) => item.href === pathname);
  const pageName = currentItem?.label ?? t("shell.settings");
  const settings = {
    id: "settings",
    label: t("shell.settings"),
    icon: SettingsIcon,
    href: personalPages.profile,
  };

  return (
    <>
      {me.data && <LocaleSync language={me.data.language} />}
      <AppShell
        // Inside Settings the shell draws the settings menu, so the head and the main menu never show.
        head={() => null}
        main={[]}
        settings={settings}
        settingsMenu={{
          back: { label: t("shell.backToMyOrganisations"), href: "/" },
          groups: [
            { id: "my-account", label: t("shell.myAccount"), items: myAccount },
          ],
        }}
        current={currentItem?.id ?? ""}
        bell={(place) => <PersonalBell place={place} />}
        account={(place) =>
          me.data && (
            <AccountMenu
              name={me.data.name ?? ""}
              email={me.data.email ?? ""}
              person={me.data}
              profileHref={personalPages.profile}
              accountAndSecurityHref={personalPages.account}
              onSignOut={() => void signOut({ redirectUrl: "/sign-in" })}
              version={version}
              variant={place}
              linkComponent={Link}
            />
          )
        }
        version={version}
        pageName={pageName}
        crumbs={[{ label: t("shell.settings") }, { label: pageName }]}
        commands={[
          {
            id: "my-account",
            label: t("shell.settingsMyAccount"),
            items: myAccount,
          },
        ]}
        bar={<ServerBar />}
        tabTitle={tabTitle(pageName, unread.data?.unread)}
        linkComponent={Link}
        onNavigate={(href) => router.push(href)}
      >
        {children}
      </AppShell>
    </>
  );
}
