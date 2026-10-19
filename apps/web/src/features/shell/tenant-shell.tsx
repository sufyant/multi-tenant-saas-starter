"use client";

import { useClerk } from "@clerk/nextjs";
import { getGetMeQueryOptions } from "@repo/api-client";
import { AppShell } from "@repo/ui/components/app-shell";
import { HomeIcon, SettingsIcon } from "@repo/ui/components/icon";
import { OrgMenu } from "@repo/ui/components/org-menu";
import { UserMenu } from "@repo/ui/components/user-menu";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useTheme } from "next-themes";
import type { ReactNode } from "react";
import { LocaleSync } from "@/shared/locale-sync";
import { ProblemScreen } from "@/shared/problem-screen";
import { useTenant } from "@/shared/tenant-context";

const initialsOf = (...names: (string | null)[]) =>
  names
    .filter((name): name is string => Boolean(name))
    .map((name) => name.charAt(0))
    .join("");

/**
 * The tenant web's AppShell (F28, F30): Overview and Settings in the rail, the breadcrumb that starts with the organisation, the
 * OrgMenu when the user has more than one organisation, the UserMenu from GetMe, and ⌘K.
 */
export function TenantShell({
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
  const { tenant, detail, hasOthers } = useTenant();
  const me = useQuery(getGetMeQueryOptions());
  if (me.error) return <ProblemScreen error={me.error} />;

  const base = `/${tenant.slug}`;
  const sections = {
    settings: "shell.settings",
    preferences: "shell.preferences",
  } as const;
  const crumbs = [
    { label: tenant.name },
    ...pathname
      .slice(base.length)
      .split("/")
      .filter(
        (segment): segment is keyof typeof sections => segment in sections,
      )
      .map((segment) => ({ label: t(sections[segment]) })),
  ];
  const role = detail.role.builtIn
    ? t(`shell.roles.${detail.role.builtIn}`)
    : detail.role.name;

  return (
    <>
      {me.data && <LocaleSync language={me.data.language} />}
      <AppShell
        nav={[
          {
            id: "overview",
            label: t("shell.overview"),
            icon: HomeIcon,
            href: base,
          },
          {
            id: "settings",
            label: t("shell.settings"),
            icon: SettingsIcon,
            href: `${base}/settings/preferences`,
            placement: "bottom",
          },
        ]}
        current={
          pathname.startsWith(`${base}/settings`) ? "settings" : "overview"
        }
        logo={tenant.name.charAt(0)}
        logoLabel={t("shell.home")}
        logoHref={base}
        crumbs={crumbs}
        orgMenu={
          hasOthers ? (
            <OrgMenu
              name={tenant.name}
              slug={tenant.slug}
              initials={tenant.name.charAt(0)}
              memberRole={role}
              switchLabel={t("shell.switchOrganisation")}
              switchHref="/"
              linkComponent={Link}
            />
          ) : undefined
        }
        userMenu={
          me.data && (
            <UserMenu
              name={[me.data.firstName, me.data.lastName]
                .filter(Boolean)
                .join(" ")}
              email={me.data.email ?? ""}
              initials={initialsOf(me.data.firstName, me.data.lastName)}
              label={t("common.account")}
              languageName={t(`languages.${me.data.language}`)}
              languageLabel={t("shell.language")}
              preferencesHref={`${base}/settings/preferences`}
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
          )
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
