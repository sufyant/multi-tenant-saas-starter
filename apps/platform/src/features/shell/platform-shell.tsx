"use client";

import { useClerk } from "@clerk/nextjs";
import {
  ErrorCode,
  getGetMeQueryOptions,
  getGetTenantQueryOptions,
  getGetUserQueryOptions,
  getListTenantsQueryOptions,
  ProblemDetailsError,
  platformBell,
} from "@repo/api-client";
import { brand } from "@repo/brand";
import { AccountMenu } from "@repo/ui/components/account-menu";
import { useAppFrame } from "@repo/ui/components/app-frame";
import {
  AppShell,
  type CommandSection,
  type NavItem,
  useTabTitleText,
} from "@repo/ui/components/app-shell";
import {
  ActivityIcon,
  BuildingIcon,
  CreditCardIcon,
  LockIcon,
  SettingsIcon,
  ShieldCheckIcon,
  UserIcon,
  UsersIcon,
} from "@repo/ui/components/icon";
import { OrgSwitcher } from "@repo/ui/components/org-switcher";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { type ReactNode, useEffect, useState } from "react";
import { LocaleSync } from "@/shared/locale-sync";
import { userNameOf } from "@/shared/name-of";
import { NoAccess } from "@/shared/no-access";
import { PlatformBell } from "@/shared/notification-bell";
import { NotificationStream } from "@/shared/notification-stream";
import { ProblemScreen } from "@/shared/problem-screen";
import { ServerBar } from "@/shared/server-bar";

/** The tenant of an organisation's page, `/organisations/<id>`, or null on any other page. */
const tenantIdOf = (pathname: string) =>
  /^\/organisations\/([^/]+)/.exec(pathname)?.[1] ?? null;

/** The user of a user's page, `/users/<id>`, or null on any other page. */
const userIdOf = (pathname: string) =>
  /^\/users\/([^/]+)/.exec(pathname)?.[1] ?? null;

const notAUserYet = (error: Error | null) =>
  error instanceof ProblemDetailsError &&
  error.problem?.code === ErrorCode.UserNotFound;

/**
 * The platform's shell (the platform's Shell screens): the product and "Platform" at the top, with no switcher, search, the
 * platform's bell, Organisations, Users, Plans and Activity log, Settings and the account menu; inside Settings the settings menu
 * with My account and Platform. A signed-in user who is no system admin sees "You don't have access"; the API refuses them anyway.
 *
 * A root system admin has no grant until their first request to the platform door with a verified second factor: the door
 * writes their grant then, and their user too when they are no user yet (`SystemAdminDirectory`,
 * `apps/api/docs/flows/system-admins.md`). So when GetMe answers 404 or a user who is no system admin, the shell asks the door
 * once, with the smallest read it has, and reads GetMe again when the door lets the person in (F42).
 */
export function PlatformShell({
  notFound = false,
  webAppUrl,
  children,
}: {
  /** "Page not found" of an address that no page has: its own crumb, and no row of the menu is the current one (2.40). */
  notFound?: boolean;
  /** `WEB_APP_URL`, which the server reads: the account menu's "Open the web app" (2.03). */
  webAppUrl?: string;
  children: ReactNode;
}) {
  const t = useTranslations();
  const router = useRouter();
  const pathname = usePathname();
  const { signOut } = useClerk();
  const { version } = useAppFrame();
  const tabTitle = useTabTitleText(t("meta.title"));
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
  const unread = useQuery({
    ...platformBell().count,
    enabled: me.data?.isSystemAdmin === true,
  });
  const { refetch } = me;
  useEffect(() => {
    if (doorAsked) return;
    if (door.isError) setDoorAsked(true);
    else if (door.isSuccess) void refetch().finally(() => setDoorAsked(true));
  }, [door.isError, door.isSuccess, doorAsked, refetch]);
  // Until GetMe and the door answer, the shell shows with what the route knows and the content waits (the ListStates boards).
  if (!askTheDoor && me.error) return <ProblemScreen error={me.error} />;
  const admin = askTheDoor ? undefined : me.data;
  if (admin && !admin.isSystemAdmin) return <NoAccess webAppUrl={webAppUrl} />;

  const main: NavItem[] = [
    {
      id: "organisations",
      label: t("shell.organisations"),
      icon: BuildingIcon,
      href: "/",
    },
    { id: "users", label: t("shell.users"), icon: UsersIcon, href: "/users" },
    {
      id: "plans",
      label: t("shell.plans"),
      icon: CreditCardIcon,
      href: "/plans",
    },
    {
      id: "activity",
      label: t("shell.activityLog"),
      icon: ActivityIcon,
      href: "/activity",
    },
  ];
  const myAccount: NavItem[] = [
    {
      id: "profile",
      label: t("appShell.profile"),
      icon: UserIcon,
      href: "/settings/profile",
    },
    {
      id: "account",
      label: t("appShell.accountAndSecurity"),
      icon: LockIcon,
      href: "/settings/account",
    },
  ];
  // Terms of use and Feature flags join in steps 38 and 39.
  const platform: NavItem[] = [
    {
      id: "system-admins",
      label: t("shell.systemAdmins"),
      icon: ShieldCheckIcon,
      href: "/settings/system-admins",
    },
  ];
  const inSettings = pathname.startsWith("/settings");
  const first = pathname.split("/")[1] ?? "";
  const current = notFound
    ? ""
    : inSettings
      ? (pathname.split("/")[2] ?? "")
      : first === ""
        ? "organisations"
        : first === "organisations"
          ? "organisations"
          : first;
  const item = [...main, ...myAccount, ...platform].find(
    (entry) => entry.id === current,
  );
  // The page of notifications, which the phone's bell opens (F57), is no row of the menu.
  const pageName = notFound
    ? t("problems.notFoundTitle")
    : current === "notifications"
      ? t("notifications.title")
      : (item?.label ?? t("shell.settings"));
  const userName = user.data && userNameOf(user.data, t("users.deletedUser"));
  const detail =
    current === "users" && userId
      ? userName
      : current === "organisations" && tenantId
        ? tenant.data?.name
        : undefined;
  const crumbs = [
    { label: inSettings ? t("shell.settings") : t("shell.platform") },
    { label: pageName, href: detail ? item?.href : undefined },
    ...(detail ? [{ label: detail }] : []),
  ];
  const commands: CommandSection[] = [
    { id: "go-to", label: t("appShell.goTo"), items: main },
    {
      id: "my-account",
      label: t("shell.settingsMyAccount"),
      items: myAccount,
    },
    { id: "platform", label: t("shell.settingsPlatform"), items: platform },
  ];

  return (
    <>
      {admin && <LocaleSync language={admin.language} />}
      {/* One live stream for the tab, once the door has made the person a system admin (F42, F57). */}
      {admin && <NotificationStream />}
      <AppShell
        head={(place) => (
          // The product and "Platform" in the switcher's face, which opens nothing (the Platform sidebar).
          <OrgSwitcher
            current={{
              id: "platform",
              // The drawer's heading names both: "Starter Platform" (the Platform phone drawer).
              name:
                place === "rail"
                  ? brand.name
                  : t("shell.productPlatform", { product: brand.name }),
              role: t("shell.platform"),
              href: "/",
            }}
            organisations={[]}
            title={t("shell.platform")}
            variant={place === "rail" ? "rail" : "drawer"}
          />
        )}
        main={main}
        settings={{
          id: "settings",
          label: t("shell.settings"),
          icon: SettingsIcon,
          href: "/settings/profile",
        }}
        settingsMenu={
          inSettings
            ? {
                back: { label: t("shell.backToPlatform"), href: "/" },
                groups: [
                  {
                    id: "my-account",
                    label: t("shell.myAccount"),
                    items: myAccount,
                  },
                  {
                    id: "platform",
                    label: t("shell.platform"),
                    items: platform,
                  },
                ],
              }
            : undefined
        }
        current={current}
        bell={(place) => admin && <PlatformBell place={place} />}
        account={(place) =>
          admin && (
            <AccountMenu
              name={admin.name ?? ""}
              email={admin.email ?? ""}
              person={{ id: admin.id, name: admin.name }}
              profileHref="/settings/profile"
              accountAndSecurityHref="/settings/account"
              webAppHref={webAppUrl}
              onSignOut={() => void signOut({ redirectUrl: "/sign-in" })}
              version={version}
              variant={place}
              linkComponent={Link}
            />
          )
        }
        version={version}
        pageName={detail ?? pageName}
        crumbs={crumbs}
        commands={commands}
        bar={<ServerBar />}
        tabTitle={tabTitle(detail ?? pageName, unread.data?.unread)}
        linkComponent={Link}
        onNavigate={(href) => router.push(href)}
      >
        {admin ? children : <Skeleton delayed label={t("common.loading")} />}
      </AppShell>
    </>
  );
}
