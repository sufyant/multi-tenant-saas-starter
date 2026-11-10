import {
  ErrorCode,
  getGetTenantPlanQueryKey,
  getGetTenantQueryKey,
  getListTenantsInfiniteQueryKey,
  getListTenantsQueryKey,
  InvitationResponseState,
  type TenantListItemResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import type { PillTone } from "@repo/ui/components/pill";
import type { QueryClient } from "@tanstack/react-query";
import { afterAccountRefusal } from "@/shared/refusal";

/** What the list and the detail page both know of an organisation: an item of ListTenants, or the answer of GetTenant. */
export type Organisation = Pick<
  TenantListItemResponse,
  | "id"
  | "name"
  | "slug"
  | "status"
  | "memberCount"
  | "ownerCount"
  | "ownerInvitation"
  | "reopenableUntil"
  | "pendingInvitationCount"
>;

/**
 * The message key and the Pill tone of each status (4.01, K4; the Pill component's description): Being deleted is Danger, Closed
 * and Deleted are Neutral. Colour is never the only signal: the word stays.
 */
export const statuses = {
  [TenantResponseStatus.Active]: {
    label: "organisations.statusActive",
    tone: "success",
  },
  [TenantResponseStatus.Suspended]: {
    label: "organisations.statusSuspended",
    tone: "warning",
  },
  [TenantResponseStatus.Closed]: {
    label: "organisations.statusClosed",
    tone: "neutral",
  },
  [TenantResponseStatus.Purging]: {
    label: "organisations.statusPurging",
    tone: "destructive",
  },
  [TenantResponseStatus.Purged]: {
    label: "organisations.statusPurged",
    tone: "neutral",
  },
} as const satisfies Record<
  TenantResponseStatus,
  { label: string; tone: PillTone }
>;

/** A tenant whose purge has begun or ended: it takes no action, and it counts no member and no Owner (K4). */
export const isGone = (organisation: Pick<Organisation, "status">) =>
  organisation.status === TenantResponseStatus.Purging ||
  organisation.status === TenantResponseStatus.Purged;

/** The Owner invitation that waits for its Owner: pending, or expired. A cancelled one waits for nothing. */
export function waitingInvitation(organisation: Organisation) {
  const invitation = organisation.ownerInvitation;
  return invitation &&
    (invitation.state === InvitationResponseState.Pending ||
      invitation.state === InvitationResponseState.Expired)
    ? invitation
    : null;
}

/** An active organisation that has no Owner: it waits for its first Owner (the OrgOwnerless board). */
export const waitsForOwner = (organisation: Organisation) =>
  organisation.status === TenantResponseStatus.Active &&
  organisation.ownerCount === 0;

export type Action =
  | "inviteOwner"
  | "rename"
  | "suspend"
  | "reactivate"
  | "close"
  | "reopen";

/**
 * The actions of an organisation by its status (K4; the Main board): active ⇄ suspended → closed → reopened, and none once the
 * purge has begun; the menu puts its separator before Close. Every system admin holds every platform permission (the API's A4), so the menu has no
 * permission check; the API still decides.
 */
export function actionsOf(organisation: Organisation): Action[] {
  switch (organisation.status) {
    case TenantResponseStatus.Active:
      return [
        ...(waitsForOwner(organisation) ? (["inviteOwner"] as const) : []),
        "rename",
        "suspend",
        "close",
      ];
    case TenantResponseStatus.Suspended:
      return ["rename", "reactivate", "close"];
    case TenantResponseStatus.Closed:
      return ["reopen"];
    default:
      return [];
  }
}

/** Every read of the list: the shell's search reads one page, the Organisations page the pages Load more added (F9). */
export const refreshOrganisations = (queryClient: QueryClient) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getListTenantsQueryKey() }),
    queryClient.invalidateQueries({
      queryKey: getListTenantsInfiniteQueryKey(),
    }),
  ]);

/** After a command the screen reads again (F47): the organisation, its plan, and every page of the list. */
export const refreshOrganisation = (queryClient: QueryClient, id: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getGetTenantQueryKey(id) }),
    queryClient.invalidateQueries({ queryKey: getGetTenantPlanQueryKey(id) }),
    refreshOrganisations(queryClient),
  ]);

/** A refusal that says the organisation changed meanwhile: the dialog shows its text, and the screen reads again (F47). */
const changedMeanwhile = new Set<string>([
  ErrorCode.TenantNotActive,
  ErrorCode.TenantNotClosed,
  ErrorCode.TenantReopenPeriodEnded,
  ErrorCode.TenantHasOwner,
  ErrorCode.TenantNotFound,
  ErrorCode.PlanOverrideNotFound,
]);

/**
 * What a refused command means beyond its dialog (F4, F47): a 401 goes to sign-in; a suspended or deleted user reads GetMe again,
 * and the shell shows the account's screen; a conflict reads the organisation and the list again. A create has no organisation yet.
 */
export function afterRefusal(
  queryClient: QueryClient,
  id: string | null,
  error: unknown,
): void {
  const code = afterAccountRefusal(queryClient, error);
  if (id && code && changedMeanwhile.has(code))
    void refreshOrganisation(queryClient, id);
}
