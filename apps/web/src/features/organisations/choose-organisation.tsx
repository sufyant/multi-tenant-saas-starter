"use client";

import {
  getGetMeQueryOptions,
  getListMyTenantsQueryOptions,
} from "@repo/api-client";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { LocaleSync } from "@/shared/locale-sync";
import { NoAccess } from "@/shared/no-access";
import { ProblemScreen } from "@/shared/problem-screen";

/**
 * "Choose an organisation" at `/` (F30). One organisation opens at once; none, or a person who is not a user yet, sees "No access
 * yet", with the same text for both.
 */
export function ChooseOrganisation() {
  const t = useTranslations();
  const router = useRouter();
  const me = useQuery(getGetMeQueryOptions());
  const tenants = useQuery(getListMyTenantsQueryOptions({ pageSize: 100 }));
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
    <main className="mx-auto flex max-w-xl flex-col gap-4 p-6">
      <LocaleSync language={me.data.language} />
      <h1 className="text-page-title">{t("organisations.chooseTitle")}</h1>
      <p className="text-body text-muted-foreground">
        {t("organisations.chooseText")}
      </p>
      <ul className="flex flex-col">
        {tenants.data.items.map((tenant) => (
          <li key={tenant.id} className="border-b border-border">
            <Link
              href={`/${tenant.slug}`}
              className="flex min-h-14 items-center gap-3 px-2 hover:bg-accent"
            >
              <span className="flex flex-1 flex-col">
                <span className="font-medium">{tenant.name}</span>
                <bdi
                  dir="ltr"
                  className="font-mono text-mono text-muted-foreground"
                >
                  {tenant.slug}
                </bdi>
              </span>
              <span className="text-body text-muted-foreground">
                {tenant.role.builtIn
                  ? t(`shell.roles.${tenant.role.builtIn}`)
                  : tenant.role.name}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </main>
  );
}
