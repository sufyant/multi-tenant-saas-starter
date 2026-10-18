"use client";

import {
  getGetMyTenantQueryOptions,
  getListMyTenantsQueryOptions,
  type MyTenantDetailResponse,
  type MyTenantResponse,
  type Permission,
  schemas,
} from "@repo/api-client";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { createContext, type ReactNode, use } from "react";
import { NoAccess } from "./no-access";
import { ProblemScreen } from "./problem-screen";

export type TenantContext = {
  tenant: MyTenantResponse;
  /** The member's view of the tenant: the role and the permissions (GetMyTenant). */
  detail: MyTenantDetailResponse;
  /** The user has more than one organisation, so the OrgMenu shows. */
  hasOthers: boolean;
};

const Context = createContext<TenantContext | null>(null);

/** The tenant of the route. Only a child of TenantProvider calls it. */
export function useTenant(): TenantContext {
  const tenant = use(Context);
  if (!tenant) throw new Error("useTenant() needs a TenantProvider above it.");
  return tenant;
}

/** `can(permission)` reads GetMyTenant.permissions (F5). The screen hides what the user may not do; the API still decides. */
export function useCan(): (permission: Permission) => boolean {
  const { detail } = useTenant();
  return (permission) => detail.permissions.includes(permission);
}

/**
 * Turns the route's slug into the tenant (F30), through the cached ListMyTenants: the API path keeps the id. A slug that is not
 * in the user's list shows "No access yet".
 */
export function TenantProvider({
  slug,
  children,
}: {
  slug: string;
  children: ReactNode;
}) {
  const t = useTranslations("common");
  const matching = useQuery(
    getListMyTenantsQueryOptions({
      q: slug,
      pageSize: schemas.listMyTenantsQueryPageSizeMax,
    }),
  );
  const all = useQuery(getListMyTenantsQueryOptions({ pageSize: 1 }));
  const tenant = matching.data?.items.find((item) => item.slug === slug);
  const detail = useQuery(
    getGetMyTenantQueryOptions(tenant?.id ?? "", {
      query: { enabled: tenant !== undefined },
    }),
  );

  const error = matching.error ?? all.error ?? detail.error;
  if (error) return <ProblemScreen error={error} />;
  if (matching.data && !tenant) return <NoAccess />;
  if (!tenant || !detail.data || !all.data)
    return <Skeleton label={t("loading")} />;
  return (
    <Context
      value={{ tenant, detail: detail.data, hasOthers: all.data.total > 1 }}
    >
      {children}
    </Context>
  );
}
