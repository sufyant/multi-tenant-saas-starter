import {
  actionFor,
  ErrorCode,
  getGetMeQueryKey,
  getGetTenantQueryKey,
  getListTenantsQueryKey,
  InvitationResponseState,
  ProblemDetailsError,
  type TenantListItemResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import type { StatusTone } from "@repo/ui/components/status";
import type { QueryClient } from "@tanstack/react-query";

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
>;

/** The message key and the tone of each status (the Main board, K4). Colour is never the only signal: the word stays. */
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
    tone: "neutral",
  },
  [TenantResponseStatus.Purged]: {
    label: "organisations.statusPurged",
    tone: "neutral",
  },
} as const satisfies Record<
  TenantResponseStatus,
  { label: string; tone: StatusTone }
>;

/** A tenant whose purge has begun or ended: it takes no action, and it counts no member and no Owner (K4). */
export const isGone = (organisation: Pick<Organisation, "status">) =>
  organisation.status === TenantResponseStatus.Purging ||
  organisation.status === TenantResponseStatus.Purged;

const day = 24 * 60 * 60 * 1000;

/**
 * The whole days until the deletion date the API answered (`reopenableUntil`), for "Deleted in 3 days": the nearest whole number,
 * and at least one, as the purge warning's email counts them (`flows/tenant-purge.md`, step 2). Never a fixed period.
 */
export function daysUntil(at: string, now: Date): number {
  return Math.max(
    1,
    Math.round((new Date(at).getTime() - now.getTime()) / day),
  );
}

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
 * purge has begun. `null` is a separator. Every system admin holds every platform permission (the API's A4), so the menu has no
 * permission check; the API still decides.
 */
export function actionsOf(organisation: Organisation): (Action | null)[] {
  switch (organisation.status) {
    case TenantResponseStatus.Active:
      return [
        ...(waitsForOwner(organisation)
          ? (["inviteOwner", null] as const)
          : []),
        "rename",
        "suspend",
        null,
        "close",
      ];
    case TenantResponseStatus.Suspended:
      return ["rename", "reactivate", null, "close"];
    case TenantResponseStatus.Closed:
      return ["reopen"];
    default:
      return [];
  }
}

/** After a command the screen reads again (F47): the organisation, and every page of the list. */
export const refreshOrganisation = (queryClient: QueryClient, id: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getGetTenantQueryKey(id) }),
    queryClient.invalidateQueries({ queryKey: getListTenantsQueryKey() }),
  ]);

/** A refusal that says the organisation changed meanwhile: the dialog shows its text, and the screen reads again (F47). */
const changedMeanwhile = new Set<string>([
  ErrorCode.TenantNotActive,
  ErrorCode.TenantNotClosed,
  ErrorCode.TenantReopenPeriodEnded,
  ErrorCode.TenantHasOwner,
  ErrorCode.TenantNotFound,
]);

/**
 * What a refused command means beyond its dialog (F4, F47): a 401 goes to sign-in; a suspended or deleted user reads GetMe again,
 * and the shell shows the account's screen; a conflict reads the organisation and the list again.
 */
export function afterRefusal(
  queryClient: QueryClient,
  id: string,
  error: unknown,
): void {
  if (!(error instanceof ProblemDetailsError)) return;
  const { kind } = actionFor(error.status, error.problem);
  if (kind === "sign-in") window.location.assign("/sign-in");
  if (kind === "account-suspended" || kind === "account-deleted")
    void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
  if (error.problem && changedMeanwhile.has(error.problem.code))
    void refreshOrganisation(queryClient, id);
}

/**
 * Whether the API saved this answer under the Idempotency-Key (I6), so the same intent sent later needs a new key (F48): a 4xx
 * answer. A request still running (409 `idempotency.key_in_progress`), a server failure and a dropped connection keep the key.
 */
export const savedUnderKey = (error: unknown) =>
  error instanceof ProblemDetailsError &&
  error.status < 500 &&
  error.problem?.code !== ErrorCode.IdempotencyKeyInProgress;
