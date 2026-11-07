"use client";

import { useClerk } from "@clerk/nextjs";
import {
  getGetMeQueryOptions,
  getListMyTenantsQueryOptions,
  type MyTenantResponse,
  MyTenantResponseStatus,
  schemas,
} from "@repo/api-client";
import { AuthHeading, AuthScreen } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { ChevronRightIcon, Icon } from "@repo/ui/components/icon";
import { Pill } from "@repo/ui/components/pill";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { forgetOrganisation } from "@/shared/last-organisation";
import { LocaleSync } from "@/shared/locale-sync";
import { NoAccess } from "@/shared/no-access";
import { PersonalBell } from "@/shared/notification-bell";
import { ProblemScreen } from "@/shared/problem-screen";
import { useRoleName } from "@/shared/use-role-name";

/**
 * "Choose an organisation" at `/` (F30, frame 1.18). One organisation opens at once; none, or a person who is not a user yet, sees
 * "No access yet", with the same text for both. The organisation of the "last used" cookie comes first and says so; a slug that
 * names no organisation the person can open is ignored, and the cookie is deleted. The personal bell shows the notifications about
 * no organisation (F57).
 */
export function ChooseOrganisation({
  lastUsed,
}: {
  /** The slug of the cookie of "last used", which only the server reads. */
  lastUsed?: string;
}) {
  const t = useTranslations();
  const roleName = useRoleName();
  const router = useRouter();
  const { signOut } = useClerk();
  const me = useQuery(getGetMeQueryOptions());
  const tenants = useQuery(
    getListMyTenantsQueryOptions({
      pageSize: schemas.listMyTenantsQueryPageSizeMax,
    }),
  );
  const items = tenants.data?.items;
  const only = tenants.data?.total === 1 ? items?.[0] : undefined;
  const last = items?.find(
    (tenant) =>
      tenant.slug === lastUsed &&
      tenant.status === MyTenantResponseStatus.Active,
  );
  const stale = Boolean(lastUsed && items && !last);

  useEffect(() => {
    if (only) router.replace(`/${only.slug}`);
  }, [only, router]);
  useEffect(() => {
    if (stale) void forgetOrganisation();
  }, [stale]);

  const error = me.error ?? tenants.error;
  if (error) return <ProblemScreen error={error} />;
  if (!me.data || !tenants.data || !items || only)
    return <Skeleton label={t("common.loading")} />;
  if (tenants.data.total === 0) return <NoAccess />;
  const ordered = last
    ? [last, ...items.filter((tenant) => tenant !== last)]
    : items;
  return (
    // The bell sits in the page's flow, under the environment strip (F59), at the top end.
    <div className="relative">
      <LocaleSync language={me.data.language} />
      <header className="absolute end-4 top-4 tablet:end-5 tablet:top-5">
        <PersonalBell />
      </header>
      <AuthScreen brand={t("meta.title")}>
        <AuthHeading
          title={t("organisations.chooseTitle")}
          text={t("organisations.chooseText", { count: tenants.data.total })}
        />
        <ul className="flex flex-col gap-3">
          {ordered.map((tenant) => (
            <li key={tenant.id}>
              <Organisation
                tenant={tenant}
                role={
                  tenant === last
                    ? t("organisations.lastUsed", {
                        role: roleName(tenant.role),
                      })
                    : roleName(tenant.role)
                }
              />
            </li>
          ))}
        </ul>
        <Button
          variant="ghost"
          className="self-center"
          onClick={() => void signOut({ redirectUrl: "/sign-in" })}
        >
          {t("common.signOut")}
        </Button>
      </AuthScreen>
    </div>
  );
}

/**
 * One organisation of the list: its initial, its name and the person's role. An organisation that is not active cannot be opened:
 * it shows its status and no link. A purging one shows "Being deleted".
 */
function Organisation({
  tenant,
  role,
}: {
  tenant: MyTenantResponse;
  role: string;
}) {
  const t = useTranslations();
  const active = tenant.status === MyTenantResponseStatus.Active;
  const row =
    "flex items-center gap-3 rounded-lg border border-border px-3 py-2.5";
  const content = (
    <>
      <span
        aria-hidden="true"
        className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-body font-medium text-primary-foreground"
      >
        {tenant.name.charAt(0)}
      </span>
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="truncate text-body font-medium">{tenant.name}</span>
        <span className="truncate text-small text-muted-foreground">
          {role}
        </span>
      </span>
    </>
  );
  if (!active)
    return (
      <div className={row}>
        {content}
        <Pill
          tone={
            tenant.status === MyTenantResponseStatus.Suspended
              ? "warning"
              : tenant.status === MyTenantResponseStatus.Purging
                ? "destructive"
                : "neutral"
          }
        >
          {t(`organisations.status.${tenant.status}`)}
        </Pill>
      </div>
    );
  return (
    <Link
      href={`/${tenant.slug}`}
      className={`${row} outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring`}
    >
      {content}
      <Icon
        icon={ChevronRightIcon}
        directional
        className="text-muted-foreground"
      />
    </Link>
  );
}
