"use client";

import { useClerk } from "@clerk/nextjs";
import {
  getGetMeQueryOptions,
  getListMembersQueryOptions,
  getListMyTenantsQueryOptions,
  type MyTenantResponse,
  MyTenantResponseStatus,
  Permission,
  schemas,
  tenantBell,
} from "@repo/api-client";
import { AccountMenu } from "@repo/ui/components/account-menu";
import { useAppFrame } from "@repo/ui/components/app-frame";
import {
  AppShell,
  type CommandSection,
  type NavItem,
  useTabTitleText,
} from "@repo/ui/components/app-shell";
import { Banner } from "@repo/ui/components/banner";
import {
  ArrowRightLeftIcon,
  HomeIcon,
  LockIcon,
  PlusIcon,
  SettingsIcon,
  UserIcon,
} from "@repo/ui/components/icon";
import { OrgSwitcher } from "@repo/ui/components/org-switcher";
import { Pill } from "@repo/ui/components/pill";
import { InlineSkeleton, Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { rememberOrganisation } from "@/shared/last-organisation";
import { LocaleSync } from "@/shared/locale-sync";
import { TenantBell } from "@/shared/notification-bell";
import { organisationPages } from "@/shared/organisation-pages";
import { ProblemScreen } from "@/shared/problem-screen";
import { ServerBar } from "@/shared/server-bar";
import { TenantProvider, useTenant } from "@/shared/tenant-context";
import { useTenantRoles } from "@/shared/use-can-act-on";
import { useRoleName } from "@/shared/use-role-name";

/**
 * The tenant web's shell (F28, F30, the Shell screens): the organisation switcher, search, the organisation's bell, Home and the
 * product's sections, Settings and the account menu; inside Settings the settings menu with My account and Organisation. A page
 * the person cannot open is not in their menu (F5).
 */
export function TenantShell({
  notFound = false,
  children,
}: {
  /** "Page not found" of an address under the organisation: its own crumb, and no row of the menu is the current one (2.40). */
  notFound?: boolean;
  children: ReactNode;
}) {
  const t = useTranslations("problems");
  const roleName = useRoleName();
  const pathname = usePathname();
  const { tenant, detail } = useTenant();
  // The organisation opened, so it is the "last used" of Choose an organisation (frame 1.18; F30). A server action that sets a
  // cookie renders the page again, so it waits until Clerk's script has loaded, as LocaleSync does (F13).
  const { loaded } = useClerk();
  useEffect(() => {
    if (loaded) void rememberOrganisation(tenant.slug);
  }, [loaded, tenant.slug]);
  const permissions = detail.permissions;
  const role = roleName(detail.role);
  const can = (permission: Permission) => permissions.includes(permission);
  // The settings menu's counts come from the lists' totals, which the API answers already: the members of one page of one, and the
  // roles of the tenant's one page that the screens read (F51). Only inside Settings, where the menu shows them.
  const inSettings = pathname.startsWith(`/${tenant.slug}/settings`);
  const members = useQuery(
    getListMembersQueryOptions(
      tenant.id,
      { pageSize: 1 },
      { query: { enabled: inSettings && can(Permission.TenantMembersRead) } },
    ),
  );
  const roles = useTenantRoles();
  // The role changed while a page is open (GetMyTenant read again): the page says so until the person opens another page.
  const shownRole = useRef(detail.role.id);
  const [changed, setChanged] = useState<{ role: string; on: string }>();
  useEffect(() => {
    if (shownRole.current === detail.role.id) return;
    shownRole.current = detail.role.id;
    setChanged({ role, on: pathname });
  }, [detail.role.id, role, pathname]);
  return (
    <TenantFrame
      slug={tenant.slug}
      notFound={notFound}
      known={{
        tenant,
        role,
        can,
        counts: {
          members: members.data?.total,
          roles: inSettings ? roles.data?.total : undefined,
        },
      }}
    >
      {changed && changed.on === pathname && (
        <div className="mb-6">
          <Banner title={t("roleChangedTitle", { role: changed.role })}>
            {t("roleChangedText", { role: changed.role })}
          </Banner>
        </div>
      )}
      {children}
    </TenantFrame>
  );
}

/**
 * An organisation's page in its shell: the tenant layout's (shell) group, and "Page not found" of an address under the
 * organisation that no page has, which stays outside that group so its 404 is decided on the server (F59).
 */
export function OrganisationShell({
  slug,
  notFound,
  children,
}: {
  slug: string;
  notFound?: boolean;
  children: ReactNode;
}) {
  return (
    <TenantProvider slug={slug} pending={<PendingTenantShell slug={slug} />}>
      <TenantShell notFound={notFound}>{children}</TenantShell>
    </TenantProvider>
  );
}

/**
 * The shell while the first reads of the organisation come (the ListStates boards): the frame from what the route knows, a short
 * skeleton where the organisation's name will be, and the page's skeleton in the content. `TenantProvider` renders it until the
 * organisation is known, or gives way to a page that replaces the shell.
 */
export function PendingTenantShell({ slug }: { slug: string }) {
  const t = useTranslations("common");
  return (
    <TenantFrame slug={slug}>
      <Skeleton delayed label={t("loading")} />
    </TenantFrame>
  );
}

/**
 * The product's own sections, after Home (the Sidebar's description). The template has none; a product adds its items here, each
 * with its permission, and the command menu lists them under Go to.
 */
export const productSections = (_base: string): NavItem[] => [];

type Known = {
  tenant: MyTenantResponse;
  role: string;
  can: (permission: Permission) => boolean;
  /** The counts after a page of the settings menu: the members and the roles (F59). */
  counts: Partial<Record<"members" | "roles", number>>;
};

/** The AppShell with what is known. Without the tenant, the name waits in place and the settings menu shows no page yet. */
function TenantFrame({
  slug,
  known,
  notFound = false,
  children,
}: {
  slug: string;
  known?: Known;
  notFound?: boolean;
  children: ReactNode;
}) {
  const t = useTranslations();
  const router = useRouter();
  const pathname = usePathname();
  const { signOut } = useClerk();
  const { version } = useAppFrame();
  const me = useQuery(getGetMeQueryOptions());
  // The bell reads the same list, so the switcher costs no request of its own.
  const tenants = useQuery(
    getListMyTenantsQueryOptions({
      pageSize: schemas.listMyTenantsQueryPageSizeMax,
    }),
  );
  const unread = useQuery({
    ...tenantBell(known?.tenant.id ?? "").count,
    enabled: known !== undefined,
  });
  const roleName = useRoleName();
  const tabTitle = useTabTitleText(t("meta.title"));
  if (me.error) return <ProblemScreen error={me.error} />;

  const base = `/${slug}`;
  const settings = `${base}/settings`;
  const can = known?.can ?? (() => false);
  const myAccount: NavItem[] = [
    {
      id: "profile",
      label: t("appShell.profile"),
      icon: UserIcon,
      href: `${settings}/profile`,
    },
    {
      id: "account",
      label: t("appShell.accountAndSecurity"),
      icon: LockIcon,
      href: `${settings}/account`,
    },
  ];
  // Billing and Usage join in step 40, with their permission (the Billing is for billing managers pattern).
  const organisation: NavItem[] = organisationPages
    .filter((page) => can(page.permission))
    .map((page) => ({
      id: page.id,
      label: t(`shell.${page.label}`),
      icon: page.icon,
      href: `${settings}/${page.id}`,
      count: known?.counts[page.id as keyof Known["counts"]],
    }));
  const home: NavItem = {
    id: "home",
    label: t("shell.home"),
    icon: HomeIcon,
    href: base,
  };
  const main = [home, ...productSections(base)];
  const inSettings = pathname.startsWith(settings);
  const page = pathname.slice(settings.length + 1).split("/")[0] ?? "";
  const current = notFound ? "" : inSettings ? page : home.id;
  const currentItem = [...main, ...myAccount, ...organisation].find(
    (item) => item.id === current,
  );
  const pageName = notFound
    ? t("problems.notFoundTitle")
    : (currentItem?.label ?? t("shell.settings"));
  const name = known?.tenant.name;

  const others = (tenants.data?.items ?? []).map((item) => ({
    id: item.id,
    name: item.name,
    role: roleName(item.role),
    href: `/${item.slug}`,
    status:
      item.status === MyTenantResponseStatus.Suspended ? (
        <Pill tone="warning">{t("shell.suspended")}</Pill>
      ) : item.status === MyTenantResponseStatus.Active ? undefined : (
        <Pill tone="neutral">{t("shell.closed")}</Pill>
      ),
  }));
  const several = (tenants.data?.total ?? 0) > 1;

  const commands: CommandSection[] = [
    { id: "go-to", label: t("appShell.goTo"), items: main },
    {
      id: "my-account",
      label: t("shell.settingsMyAccount"),
      items: myAccount,
    },
    {
      id: "organisation",
      label: t("shell.settingsOrganisation"),
      items: organisation,
    },
    {
      id: "actions",
      label: t("appShell.actions"),
      items: [
        ...(can(Permission.TenantInvitationsCreate) &&
        can(Permission.TenantMembersRead)
          ? [
              {
                id: "invite",
                label: t("shell.inviteMember"),
                icon: PlusIcon,
                href: `${settings}/members?invite=1`,
              },
            ]
          : []),
        ...(several
          ? [
              {
                id: "switch",
                label: t("shell.switchOrganisation"),
                icon: ArrowRightLeftIcon,
                href: "/",
              },
            ]
          : []),
      ],
    },
  ].filter((section) => section.items.length > 0);

  return (
    <>
      {me.data && <LocaleSync language={me.data.language} />}
      <AppShell
        head={(place) =>
          known ? (
            <OrgSwitcher
              current={{
                id: known.tenant.id,
                name: known.tenant.name,
                role: known.role,
                href: base,
              }}
              organisations={several ? others : []}
              title={t("shell.yourOrganisations")}
              // The drawer passes "row": the organisation's name heads its menu (the phone's drawer).
              variant={place === "rail" ? "rail" : "drawer"}
              linkComponent={Link}
            />
          ) : (
            // The organisation's name is the head; until it is known, a short skeleton holds its place.
            <div className="flex h-11 items-center px-1.5">
              <InlineSkeleton />
            </div>
          )
        }
        main={main}
        settings={{
          id: "settings",
          label: t("shell.settings"),
          icon: SettingsIcon,
          // Profile: the first page of Settings that every person can open (F5).
          href: `${settings}/profile`,
        }}
        settingsMenu={
          inSettings
            ? {
                back: {
                  label: t("shell.backTo", { organisation: name ?? "" }),
                  href: base,
                },
                groups: [
                  {
                    id: "my-account",
                    label: t("shell.myAccount"),
                    items: myAccount,
                  },
                  {
                    id: "organisation",
                    label: t("shell.organisation"),
                    items: organisation,
                  },
                ].filter((group) => group.items.length > 0),
              }
            : undefined
        }
        current={current}
        bell={(place) =>
          known && <TenantBell tenantId={known.tenant.id} place={place} />
        }
        account={(place) =>
          me.data && (
            <AccountMenu
              name={me.data.name ?? ""}
              email={me.data.email ?? ""}
              person={{ name: me.data.name }}
              profileHref={`${settings}/profile`}
              accountAndSecurityHref={`${settings}/account`}
              onSignOut={() => void signOut({ redirectUrl: "/sign-in" })}
              version={version}
              variant={place}
              linkComponent={Link}
            />
          )
        }
        version={version}
        pageName={pageName}
        crumbs={
          inSettings
            ? [{ label: t("shell.settings") }, { label: pageName }]
            : [{ label: name ?? "" }, { label: pageName }]
        }
        commands={commands}
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
