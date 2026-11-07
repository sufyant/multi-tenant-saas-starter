"use client";

import {
  actionFor,
  getGetMeQueryOptions,
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
import { Dialog } from "@repo/ui/components/dialog";
import { NotFound } from "@repo/ui/components/not-found";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import {
  createContext,
  type ReactNode,
  use,
  useEffect,
  useRef,
  useState,
} from "react";
import {
  BlockedOrganisation,
  type BlockedStatus,
} from "./blocked-organisation";
import { NoAccess } from "./no-access";
import { useOrganisationAddress } from "./organisation-address";
import { accountBlockOf } from "./problem";
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

/** True when the value is the id, or an array or object that holds it at any depth. */
function holds(value: unknown, id: string): boolean {
  if (value === id) return true;
  if (typeof value !== "object" || value === null) return false;
  return Object.values(value).some((item) => holds(item, id));
}

/**
 * Turns the route's slug into the tenant (F30), through the cached ListMyTenants: the API path keeps the id. A slug that is not
 * in the user's list shows "Organisation not found", or "No access yet" to a user without any organisation. A suspended or closed
 * organisation shows its page in place of the shell; when it stops while its page is open, a dialog says so first. When a call
 * of this organisation answers that it stopped, the list is read again, so the page follows (F47). When a read answers that the
 * user is suspended or deleted (A9), the account's page shows in place of the shell; a refused command reads GetMe again first.
 */
export function TenantProvider({
  slug,
  pending = null,
  children,
}: {
  slug: string;
  /** What shows while the first reads come: the shell with what the route knows (the ListStates boards). */
  pending?: ReactNode;
  children: ReactNode;
}) {
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

  const [accountError, setAccountError] = useState<Error | null>(null);
  // The last organisation this provider showed: when it stops while open, its page stays under the dialog until Close.
  const shown = useRef<TenantContext | null>(null);
  const [closedDialog, setClosedDialog] = useState(false);
  const tenantId = tenant?.id;
  useEffect(() => {
    // Only a call of this organisation counts for the organisation: its query key or its command's variables hold the tenant's id.
    const reread = (error: Error, call: unknown) => {
      if (tenantId && blockedBy(error) && holds(call, tenantId))
        void queryClient.invalidateQueries({
          queryKey: getListMyTenantsQueryKey(),
        });
    };
    // Any read that refuses the user's account shows its page. A refused command reads GetMe again, and its answer decides (A9).
    const queries = queryClient.getQueryCache().subscribe((event) => {
      if (event.type !== "updated" || event.action.type !== "error") return;
      if (accountBlockOf(event.action.error))
        setAccountError(event.action.error);
      reread(event.action.error, event.query.queryKey);
    });
    const commands = queryClient.getMutationCache().subscribe((event) => {
      if (event.type !== "updated" || event.action.type !== "error") return;
      if (accountBlockOf(event.action.error))
        queryClient
          .fetchQuery({ ...getGetMeQueryOptions(), staleTime: 0 })
          .catch(() => {
            // The query cache holds the refusal, and the subscription above shows the page.
          });
      reread(event.action.error, event.mutation.state.variables);
    });
    return () => {
      queries();
      commands();
    };
  }, [queryClient, tenantId]);

  if (accountError) return <ProblemScreen error={accountError} />;
  const error = matching.error ?? all.error;
  if (error) return <ProblemScreen error={error} />;
  if (matching.data && !tenant)
    return all.data?.total === 0 ? (
      <NoAccess />
    ) : (
      <OrganisationNotFound slug={slug} />
    );
  if (!tenant || !all.data) return pending;
  // The API refuses a member of an organisation whose deletion has started as it refuses a closed one's (403 `tenant.closed`).
  const blocked =
    tenant.status === MyTenantResponseStatus.Active
      ? blockedBy(detail.error)
      : tenant.status === MyTenantResponseStatus.Purging
        ? MyTenantResponseStatus.Closed
        : tenant.status;
  if (blocked && shown.current && !closedDialog)
    return (
      <Context value={shown.current}>
        {children}
        <StoppedWhileOpen
          name={tenant.name}
          status={blocked}
          onClose={() => setClosedDialog(true)}
        />
      </Context>
    );
  if (blocked)
    return (
      <BlockedOrganisation
        tenant={tenant}
        status={blocked}
        hasOthers={all.data.total > 1}
      />
    );
  if (detail.error) return <ProblemScreen error={detail.error} />;
  if (!detail.data) return pending;
  shown.current = {
    tenant,
    detail: detail.data,
    hasOthers: all.data.total > 1,
  };
  return <Context value={shown.current}>{children}</Context>;
}

/** The dialog of an organisation that stopped while its page was open (the Suspended while open screen). Close shows its page. */
function StoppedWhileOpen({
  name,
  status,
  onClose,
}: {
  name: string;
  status: BlockedStatus;
  onClose: () => void;
}) {
  const t = useTranslations();
  const suspended = status === MyTenantResponseStatus.Suspended;
  return (
    <Dialog
      defaultOpen
      title={t(
        suspended ? "problems.suspendedOpenTitle" : "problems.closedOpenTitle",
        { organisation: name },
      )}
      description={t(
        suspended ? "problems.suspendedOpenText" : "problems.closedOpenText",
      )}
      actionLabel={t("common.close")}
      onConfirm={onClose}
      onOpenChange={(open) => !open && onClose()}
    />
  );
}

/** "Organisation not found": an address that no organisation of the person has (the Organisation not found screen). */
function OrganisationNotFound({ slug }: { slug: string }) {
  const address = useOrganisationAddress(slug);
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl items-center p-6">
      <NotFound
        kind="organisation"
        address={address}
        href="/"
        linkComponent={Link}
      />
    </main>
  );
}
