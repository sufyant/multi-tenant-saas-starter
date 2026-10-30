"use client";

import { getListMyTenantsQueryOptions, schemas } from "@repo/api-client";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect } from "react";

/**
 * `/settings/notifications`, the address without an organisation (the maintainer's decision in step 33-frontend): the page of the
 * one organisation, or the start page, which offers the choice of organisations or says "No access yet". The preferences are the
 * person's, the same in every organisation, so any organisation's page shows them.
 */
export function ManageNotifications() {
  const t = useTranslations("common");
  const router = useRouter();
  const tenants = useQuery(
    getListMyTenantsQueryOptions({
      pageSize: schemas.listMyTenantsQueryPageSizeMax,
    }),
  );
  const only = tenants.data?.total === 1 ? tenants.data.items[0] : undefined;
  const settled = tenants.isSuccess || tenants.isError;

  useEffect(() => {
    if (!settled) return;
    router.replace(only ? `/${only.slug}/settings/notifications` : "/");
  }, [settled, only, router]);

  return <Skeleton label={t("loading")} />;
}
