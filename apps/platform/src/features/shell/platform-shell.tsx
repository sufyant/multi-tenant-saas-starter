"use client";

import { useClerk } from "@clerk/nextjs";
import {
  ErrorCode,
  getGetMeQueryOptions,
  getListTenantsQueryOptions,
  ProblemDetailsError,
} from "@repo/api-client";
import { AppShell } from "@repo/ui/components/app-shell";
import { HomeIcon, SettingsIcon } from "@repo/ui/components/icon";
import { Skeleton } from "@repo/ui/components/skeleton";
import { UserMenu } from "@repo/ui/components/user-menu";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useTheme } from "next-themes";
import { type ReactNode, useEffect, useState } from "react";
import { LocaleSync } from "@/shared/locale-sync";
import { NoAccess } from "@/shared/no-access";
import { ProblemScreen } from "@/shared/problem-screen";

const initialsOf = (...names: (string | null)[]) =>
  names
    .filter((name): name is string => Boolean(name))
    .map((name) => name.charAt(0))
    .join("");

const notAUserYet = (error: Error | null) =>
  error instanceof ProblemDetailsError &&
  error.problem?.code === ErrorCode.UserNotFound;

/**
 * The platform's AppShell: the ink "Platform" label starts the top bar, and there is no organisation and no OrgMenu. A
 * signed-in user who is no system admin sees "You don't have access"; the API refuses them anyway.
 *
 * The first system admin is no user until their first request to the platform door with a verified second factor: the door
 * writes their user and their grant then (`SystemAdminDirectory`, `apps/api/docs/flows/system-admins.md`). So when GetMe answers 404, the shell asks
 * the door once, with the smallest read it has, and reads GetMe again when the door lets the person in (F42).
 */
export function PlatformShell({
  railExpanded,
  children,
}: {
  /** The rail shows its labels on desktop: the layout read the cookie (F49). */
  railExpanded: boolean;
  children: ReactNode;
}) {
  const t = useTranslations();
  const router = useRouter();
  const pathname = usePathname();
  const { signOut } = useClerk();
  const { theme = "system", setTheme } = useTheme();
  const me = useQuery(getGetMeQueryOptions());
  const door = useQuery(
    getListTenantsQueryOptions(
      { pageSize: 1 },
      { query: { enabled: notAUserYet(me.error), retry: false } },
    ),
  );
  // GetMe is read once more after the door let the person in. A 404 after that is final: "You don't have access".
  const [askedAgain, setAskedAgain] = useState(false);
  const { refetch } = me;
  useEffect(() => {
    if (door.isSuccess && !askedAgain)
      void refetch().finally(() => setAskedAgain(true));
  }, [door.isSuccess, askedAgain, refetch]);
  if (notAUserYet(me.error) && !door.isError && !askedAgain)
    return <Skeleton label={t("common.loading")} />;
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
          railToggle: t("shell.railToggle"),
          railShortcut: t("shell.railShortcut"),
        }}
        railExpanded={railExpanded}
        linkComponent={Link}
        onNavigate={(href) => router.push(href)}
      >
        {children}
      </AppShell>
    </>
  );
}
