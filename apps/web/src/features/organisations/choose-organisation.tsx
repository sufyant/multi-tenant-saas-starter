"use client";

import {
  getGetMeQueryOptions,
  getListMyTenantsQueryOptions,
  MyTenantResponseStatus,
  schemas,
} from "@repo/api-client";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Status } from "@repo/ui/components/status";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { LocaleSync } from "@/shared/locale-sync";
import { NoAccess } from "@/shared/no-access";
import { PersonalBell } from "@/shared/notification-bell";
import { ProblemScreen } from "@/shared/problem-screen";
import { useRoleName } from "@/shared/use-role-name";

/**
 * "Choose an organisation" at `/` (F30). One organisation opens at once; none, or a person who is not a user yet, sees "No access
 * yet", with the same text for both. The personal bell shows the notifications about no organisation (the SelectTenantBell board).
 */
export function ChooseOrganisation() {
  const t = useTranslations();
  const roleName = useRoleName();
  const router = useRouter();
  const me = useQuery(getGetMeQueryOptions());
  const tenants = useQuery(
    getListMyTenantsQueryOptions({
      pageSize: schemas.listMyTenantsQueryPageSizeMax,
    }),
  );
  const only = tenants.data?.total === 1 ? tenants.data.items[0] : undefined;

  useEffect(() => {
    if (only) router.replace(`/${only.slug}`);
  }, [only, router]);

  const error = me.error ?? tenants.error;
  if (error) return <ProblemScreen error={error} />;
  if (!me.data || !tenants.data || only)
    return <Skeleton label={t("common.loading")} />;
  if (tenants.data.total === 0) return <NoAccess />;
  return (
    <>
      <header className="flex justify-end px-4 pt-4 tablet:px-6 tablet:pt-5">
        <PersonalBell />
      </header>
      <main className="mx-auto flex max-w-xl flex-col gap-4 p-6">
        <LocaleSync language={me.data.language} />
        <h1 className="text-page-title">{t("organisations.chooseTitle")}</h1>
        <p className="text-body text-muted-foreground">
          {t("organisations.chooseText", { count: tenants.data.total })}
        </p>
        <ul className="flex flex-col overflow-hidden rounded-xl border border-border bg-card shadow-xs">
          {tenants.data.items.map((tenant) => {
            const active = tenant.status === MyTenantResponseStatus.Active;
            const row = (
              <>
                <span
                  aria-hidden="true"
                  className="inline-flex size-10 shrink-0 items-center justify-center rounded-lg bg-muted font-semibold"
                >
                  {tenant.name.charAt(0)}
                </span>
                <span
                  className={`flex min-w-0 flex-1 flex-col ${active ? "" : "opacity-60"}`}
                >
                  <span className="font-medium">{tenant.name}</span>
                  <span className="text-small text-muted-foreground">
                    <bdi dir="ltr" className="font-mono">
                      {tenant.slug}
                    </bdi>
                    {" · "}
                    {roleName(tenant.role)}
                  </span>
                </span>
              </>
            );
            // An organisation that is not active cannot be opened (the SelectTenant board): it shows its status and no link. A purging
            // one shows "Being deleted".
            return (
              <li
                key={tenant.id}
                className="border-b border-border last:border-b-0"
              >
                {active ? (
                  <Link
                    href={`/${tenant.slug}`}
                    className="flex min-h-16 items-center gap-3 px-4 py-3 hover:bg-accent"
                  >
                    {row}
                  </Link>
                ) : (
                  <div className="flex min-h-16 items-center gap-3 px-4 py-3">
                    {row}
                    <Status
                      tone={
                        tenant.status === MyTenantResponseStatus.Suspended
                          ? "warning"
                          : "neutral"
                      }
                    >
                      {t(`organisations.status.${tenant.status}`)}
                    </Status>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      </main>
    </>
  );
}
