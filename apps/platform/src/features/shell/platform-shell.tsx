"use client";

import { useClerk } from "@clerk/nextjs";
import { getGetMeQueryOptions } from "@repo/api-client";
import { AppShell } from "@repo/ui/components/app-shell";
import { HomeIcon, SettingsIcon } from "@repo/ui/components/icon";
import { Skeleton } from "@repo/ui/components/skeleton";
import { UserMenu } from "@repo/ui/components/user-menu";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useTheme } from "next-themes";
import type { ReactNode } from "react";
import { LocaleSync } from "@/shared/locale-sync";
import { NoAccess } from "@/shared/no-access";
import { ProblemScreen } from "@/shared/problem-screen";

const initialsOf = (...names: (string | null)[]) =>
  names
    .filter((name): name is string => Boolean(name))
    .map((name) => name.charAt(0))
    .join("");

/**
 * The platform's AppShell: the ink "Platform" label starts the top bar, and there is no organisation and no OrgMenu. A
 * signed-in user who is no system admin sees "No access yet"; the API refuses them anyway.
 */
export function PlatformShell({ children }: { children: ReactNode }) {
  const t = useTranslations();
  const router = useRouter();
  const pathname = usePathname();
  const { signOut } = useClerk();
  const { theme = "system", setTheme } = useTheme();
  const me = useQuery(getGetMeQueryOptions());
  if (me.error) return <ProblemScreen error={me.error} />;
  if (!me.data) return <Skeleton label={t("common.loading")} />;
  if (!me.data.isSystemAdmin) return <NoAccess />;

  const sections = {
    settings: "shell.settings",
    preferences: "shell.preferences",
  } as const;
  const crumbs = [
    { label: t("shell.overview"), href: "/" },
    ...pathname
      .split("/")
      .filter(
        (segment): segment is keyof typeof sections => segment in sections,
      )
      .map((segment) => ({ label: t(sections[segment]) })),
  ];

  return (
    <>
      <LocaleSync language={me.data.language} />
      <AppShell
        nav={[
          {
            id: "overview",
            label: t("shell.overview"),
            icon: HomeIcon,
            href: "/",
          },
          {
            id: "settings",
            label: t("shell.settings"),
            icon: SettingsIcon,
            href: "/settings/preferences",
            placement: "bottom",
          },
        ]}
        current={pathname.startsWith("/settings") ? "settings" : "overview"}
        logo={t("shell.platform").charAt(0)}
        logoLabel={t("shell.home")}
        logoHref="/"
        platformLabel={t("shell.platform")}
        crumbs={crumbs}
        userMenu={
          <UserMenu
            name={[me.data.firstName, me.data.lastName]
              .filter(Boolean)
              .join(" ")}
            email={me.data.email ?? ""}
            initials={initialsOf(me.data.firstName, me.data.lastName)}
            label={t("common.account")}
            languageName={t(`languages.${me.data.language}`)}
            languageLabel={t("shell.language")}
            preferencesHref="/settings/preferences"
            theme={theme}
            themes={[
              { id: "light", label: t("theme.light") },
              { id: "dark", label: t("theme.dark") },
              { id: "system", label: t("theme.system") },
            ]}
            themeLabel={t("theme.label")}
            onThemeChange={setTheme}
            signOutLabel={t("common.signOut")}
            onSignOut={() => void signOut({ redirectUrl: "/sign-in" })}
            linkComponent={Link}
          />
        }
        labels={{
          navigation: t("shell.navigation"),
          menu: t("shell.menu"),
          search: t("shell.search"),
          searchShortcut: t("shell.searchShortcut"),
          breadcrumb: t("shell.breadcrumb"),
          commandTitle: t("shell.commandTitle"),
          commandPlaceholder: t("shell.commandPlaceholder"),
          commandEmpty: t("shell.commandEmpty"),
          close: t("common.close"),
        }}
        linkComponent={Link}
        onNavigate={(href) => router.push(href)}
      >
        {children}
      </AppShell>
    </>
  );
}
