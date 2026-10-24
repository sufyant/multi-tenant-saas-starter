"use client";

import { useClerk } from "@clerk/nextjs";
import {
  ErrorCode,
  getGetMeQueryOptions,
  getGetTenantQueryOptions,
  getGetUserQueryOptions,
  getListTenantsQueryOptions,
  ProblemDetailsError,
  UserResponseStatus,
} from "@repo/api-client";
import { AppShell } from "@repo/ui/components/app-shell";
import {
  BuildingIcon,
  SettingsIcon,
  UsersIcon,
} from "@repo/ui/components/icon";
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

/** The tenant of an organisation's page, `/organisations/<id>`, or null on any other page. */
const tenantIdOf = (pathname: string) =>
  /^\/organisations\/([^/]+)/.exec(pathname)?.[1] ?? null;

/** The user of a user's page, `/users/<id>`, or null on any other page. */
const userIdOf = (pathname: string) =>
  /^\/users\/([^/]+)/.exec(pathname)?.[1] ?? null;

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
 * A root system admin has no grant until their first request to the platform door with a verified second factor: the door
 * writes their grant then, and their user too when they are no user yet (`SystemAdminDirectory`,
 * `apps/api/docs/flows/system-admins.md`). So when GetMe answers 404 or a user who is no system admin, the shell asks the door
 * once, with the smallest read it has, and reads GetMe again when the door lets the person in (F42).
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
  // One ask of the door per page load, then one more read of GetMe when it lets the person in (F42).
  const [doorAsked, setDoorAsked] = useState(false);
  const askTheDoor =
    !doorAsked && (notAUserYet(me.error) || me.data?.isSystemAdmin === false);
  const door = useQuery(
    getListTenantsQueryOptions(
      { pageSize: 1 },
      { query: { enabled: askTheDoor, retry: false } },
    ),
  );
  // The organisation's page reads the same GetTenant, so its name in the breadcrumb comes from the cache.
  const tenantId = tenantIdOf(pathname);
  const tenant = useQuery(
    getGetTenantQueryOptions(tenantId ?? "", {
      query: { enabled: tenantId !== null && me.data?.isSystemAdmin === true },
    }),
  );
  // The user's page reads the same GetUser, so their name in the breadcrumb comes from the cache.
  const userId = userIdOf(pathname);
  const user = useQuery(
    getGetUserQueryOptions(userId ?? "", {
      query: { enabled: userId !== null && me.data?.isSystemAdmin === true },
    }),
  );
  const { refetch } = me;
  useEffect(() => {
    if (doorAsked) return;
    if (door.isError) setDoorAsked(true);
    else if (door.isSuccess) void refetch().finally(() => setDoorAsked(true));
  }, [door.isError, door.isSuccess, doorAsked, refetch]);
  if (askTheDoor) return <Skeleton label={t("common.loading")} />;
  if (me.error) return <ProblemScreen error={me.error} />;
  if (!me.data) return <Skeleton label={t("common.loading")} />;
  if (!me.data.isSystemAdmin) return <NoAccess />;

  const sections = {
    settings: "shell.settings",
    preferences: "shell.preferences",
  } as const;
  const inSettings = pathname.startsWith("/settings");
  const inUsers = pathname === "/users" || pathname.startsWith("/users/");
  const userName =
    user.data &&
    (user.data.status === UserResponseStatus.Deleted
      ? t("users.deletedUser")
      : [user.data.firstName, user.data.lastName].filter(Boolean).join(" "));
  const crumbs = inSettings
    ? pathname
        .split("/")
        .filter(
          (segment): segment is keyof typeof sections => segment in sections,
        )
        .map((segment) => ({ label: t(sections[segment]) }))
    : inUsers
      ? [
          { label: t("shell.users") },
          ...(userId && userName ? [{ label: userName }] : []),
        ]
      : [
          { label: t("shell.organisations") },
          ...(tenantId && tenant.data ? [{ label: tenant.data.name }] : []),
        ];

  return (
    <>
      <LocaleSync language={me.data.language} />
      <AppShell
        nav={[
          {
            id: "organisations",
            label: t("shell.organisations"),
            icon: BuildingIcon,
            href: "/",
          },
          {
            id: "users",
            label: t("shell.users"),
            icon: UsersIcon,
            href: "/users",
          },
          {
            id: "settings",
            label: t("shell.settings"),
            icon: SettingsIcon,
            href: "/settings/preferences",
            placement: "bottom",
          },
        ]}
        current={inSettings ? "settings" : inUsers ? "users" : "organisations"}
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
