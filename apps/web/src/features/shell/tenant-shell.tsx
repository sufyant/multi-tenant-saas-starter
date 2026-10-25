"use client";

import { useClerk } from "@clerk/nextjs";
import { getGetMeQueryOptions, type MyTenantResponse } from "@repo/api-client";
import { AppShell } from "@repo/ui/components/app-shell";
import { initialsOf } from "@repo/ui/components/avatar";
import { HomeIcon, SettingsIcon } from "@repo/ui/components/icon";
import { OrgMenu } from "@repo/ui/components/org-menu";
import { InlineSkeleton, Skeleton } from "@repo/ui/components/skeleton";
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
import { useRoleName } from "@/shared/use-role-name";

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
  const roleName = useRoleName();
  const { tenant, detail, hasOthers } = useTenant();
  return (
    <TenantFrame
      slug={tenant.slug}
      known={{ tenant, role: roleName(detail.role), hasOthers }}
      railExpanded={railExpanded}
    >
      {children}
    </TenantFrame>
  );
}

/**
 * The shell while the first reads of the organisation come (the ListStates boards): the rail and the top bar from what the route
 * knows, a short skeleton where the organisation's name will be, and the page's skeleton in the content. `TenantProvider` renders
 * it until the organisation is known, or gives way to a page that replaces the shell.
 */
export function PendingTenantShell({
  slug,
  railExpanded,
}: {
  slug: string;
  railExpanded: boolean;
}) {
  const t = useTranslations("common");
  return (
    <TenantFrame slug={slug} railExpanded={railExpanded}>
      <Skeleton delayed label={t("loading")} />
    </TenantFrame>
  );
}

/** The AppShell with what is known. Without the tenant, the name waits in place and there is no OrgMenu yet. */
function TenantFrame({
  slug,
  known,
  railExpanded,
  children,
}: {
  slug: string;
  known?: { tenant: MyTenantResponse; role: string; hasOthers: boolean };
  railExpanded: boolean;
  children: ReactNode;
}) {
  const t = useTranslations();
  const router = useRouter();
  const pathname = usePathname();
  const { signOut } = useClerk();
  const { theme = "system", setTheme } = useTheme();
  const me = useQuery(getGetMeQueryOptions());
  if (me.error) return <ProblemScreen error={me.error} />;

  const base = `/${slug}`;
  const sections = {
    settings: "shell.settings",
    general: "settings.general",
    members: "shell.members",
    roles: "settings.roles",
    activity: "settings.activity",
    preferences: "shell.preferences",
  } as const;
  const crumbs = [
    { label: known?.tenant.name ?? "" },
    ...pathname
      .slice(base.length)
      .split("/")
      .filter(
        (segment): segment is keyof typeof sections => segment in sections,
      )
      .map((segment) => ({ label: t(sections[segment]) })),
  ];
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
            // General: the first SubNav item that every role can read (F5).
            href: `${base}/settings/general`,
            placement: "bottom",
          },
        ]}
        current={
          pathname.startsWith(`${base}/settings`) ? "settings" : "overview"
        }
        logo={known?.tenant.name.charAt(0)}
        logoLabel={t("shell.home")}
        logoHref={base}
        crumbs={crumbs}
        orgMenu={
          // The organisation's name is the first crumb; until it is known, a short skeleton holds its place.
          !known ? (
            <InlineSkeleton />
          ) : known.hasOthers ? (
            <OrgMenu
              name={known.tenant.name}
              slug={known.tenant.slug}
              initials={known.tenant.name.charAt(0)}
              memberRole={known.role}
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
              initials={initialsOf({
                firstName: me.data.firstName,
                lastName: me.data.lastName,
              })}
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
