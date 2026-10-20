"use client";

import {
  actionFor,
  getGetMyTenantQueryOptions,
  getListMyTenantsQueryKey,
  getListMyTenantsQueryOptions,
  type MyTenantDetailResponse,
  type MyTenantResponse,
  MyTenantResponseStatus,
  type Permission,
  ProblemDetailsError,
  schemas,
} from "@repo/api-client";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { createContext, type ReactNode, use, useEffect } from "react";
import {
  BlockedOrganisation,
  type BlockedStatus,
} from "./blocked-organisation";
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

/** The status of an organisation that an error answer says stopped: 403 `tenant.suspended` or `tenant.closed` (F4). */
function blockedBy(error: unknown): BlockedStatus | null {
  if (!(error instanceof ProblemDetailsError)) return null;
  const action = actionFor(error.status, error.problem);
  return action.kind === "tenant-blocked" ? action.status : null;
}

/**
 * Turns the route's slug into the tenant (F30), through the cached ListMyTenants: the API path keeps the id. A slug that is not
 * in the user's list shows "No access yet". A suspended or closed organisation shows its page in place of the shell. When any
 * call of a page answers that the organisation stopped, the list is read again, so the page follows (F47).
 */
export function TenantProvider({
  slug,
  children,
}: {
  slug: string;
  children: ReactNode;
}) {
  const t = useTranslations("common");
  const queryClient = useQueryClient();
  const matching = useQuery(
    getListMyTenantsQueryOptions({
      q: slug,
      pageSize: schemas.listMyTenantsQueryPageSizeMax,
    }),
  );
  const all = useQuery(getListMyTenantsQueryOptions({ pageSize: 1 }));
  const tenant = matching.data?.items.find((item) => item.slug === slug);
  const active = tenant?.status === MyTenantResponseStatus.Active;
  const detail = useQuery(
    getGetMyTenantQueryOptions(tenant?.id ?? "", {
      query: { enabled: active },
    }),
  );

  useEffect(() => {
    const reread = (error: unknown) => {
      if (blockedBy(error))
        void queryClient.invalidateQueries({
          queryKey: getListMyTenantsQueryKey(),
        });
    };
    const queries = queryClient.getQueryCache().subscribe((event) => {
      if (event.type === "updated" && event.action.type === "error")
        reread(event.action.error);
    });
    const commands = queryClient.getMutationCache().subscribe((event) => {
      if (event.type === "updated" && event.action.type === "error")
        reread(event.action.error);
    });
    return () => {
      queries();
      commands();
    };
  }, [queryClient]);

  const error = matching.error ?? all.error;
  if (error) return <ProblemScreen error={error} />;
  if (matching.data && !tenant) return <NoAccess />;
  if (!tenant || !all.data) return <Skeleton label={t("loading")} />;
  const blocked =
    tenant.status === MyTenantResponseStatus.Active
      ? blockedBy(detail.error)
      : tenant.status;
  if (blocked)
    return (
      <BlockedOrganisation
        tenant={tenant}
        status={blocked}
        hasOthers={all.data.total > 1}
      />
    );
  if (detail.error) return <ProblemScreen error={detail.error} />;
  if (!detail.data) return <Skeleton label={t("loading")} />;
  return (
    <Context
      value={{ tenant, detail: detail.data, hasOthers: all.data.total > 1 }}
    >
      {children}
    </Context>
  );
}
