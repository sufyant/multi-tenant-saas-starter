"use client";

import {
  getListMyTenantsQueryOptions,
  MyTenantResponseStatus,
  schemas,
} from "@repo/api-client";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect } from "react";

/**
 * `/settings/notifications`, the address without an organisation (the maintainer's decision in step 33-frontend): the page of the
 * first active organisation in ListMyTenants' order, or, with none, the start page, which says "No access yet" or shows the
 * organisations that cannot open. The preferences are the person's, the same in every organisation, so any organisation's page
 * shows them.
 */
export function ManageNotifications() {
  const t = useTranslations("common");
  const router = useRouter();
  const tenants = useQuery(
    getListMyTenantsQueryOptions({
      pageSize: schemas.listMyTenantsQueryPageSizeMax,
    }),
  );
  const first = tenants.data?.items.find(
    (tenant) => tenant.status === MyTenantResponseStatus.Active,
  );
  const settled = tenants.isSuccess || tenants.isError;

  useEffect(() => {
    if (!settled) return;
    router.replace(first ? `/${first.slug}/settings/notifications` : "/");
  }, [settled, first, router]);

  return <Skeleton label={t("loading")} />;
}
