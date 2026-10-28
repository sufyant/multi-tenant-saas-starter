import {
  AuditActorResponseKind,
  type AuditDetailsResponse,
  type AuditEntryResponse,
  AuditOperation,
  type AuditRoleResponse,
  type AuditTargetResponse,
  AuditTargetResponseKind,
  BuiltInRole,
  type CursorPageOfAuditEntryResponse,
  MemberInvitedDetailsResponseOperation,
  MemberRemovedDetailsResponseOperation,
  MemberRoleChangedDetailsResponseOperation,
  Permission,
  RoleCreatedDetailsResponseOperation,
  RoleUpdatedDetailsResponseOperation,
  TenantCreatedDetailsResponseOperation,
  TenantEntitlementOverrideRemovedDetailsResponseOperation,
  TenantEntitlementOverrideSetDetailsResponseOperation,
  TenantOwnerInvitedDetailsResponseOperation,
  TenantPlanChangedDetailsResponseOperation,
  TenantPurgeWarnedDetailsResponseOperation,
  TenantRenamedDetailsResponseOperation,
  TenantSuspendedDetailsResponseOperation,
} from "@repo/api-client";
import { getListAuditEntriesMockHandler } from "@repo/api-client/msw";

// The tenant log's entries of the Activity board, and the answers of ListAuditEntries.

const role = (name: BuiltInRole) => ({
  id: "0198f0a2-0000-7000-8000-0000000000f1",
  name,
  builtIn: true,
});

/** Northwind's custom role, as an entry keeps it. */
export const billingLead: AuditRoleResponse = {
  id: "0198f0a2-0000-7000-8000-0000000000f9",
  name: "Billing lead",
  builtIn: false,
};

/** The target of a role's entry: the role, with its name at that moment (K3). */
export const roleTarget = (name = billingLead.name): AuditTargetResponse => ({
  kind: AuditTargetResponseKind.Role,
  id: billingLead.id,
  name,
  deleted: null,
});

/** An entry of the tenant log: Ayşe acted on Khalid. */
export const entry = (
  overrides: Partial<AuditEntryResponse> = {},
): AuditEntryResponse => ({
  id: "0198f0a2-0000-7000-8000-0000000000e1",
  occurredAt: "2026-10-16T13:32:00Z",
  operation: AuditOperation.MemberJoined,
  details: null,
  actor: {
    kind: AuditActorResponseKind.User,
    userId: "0198f0a2-0000-7000-8000-000000000001",
    displayName: "Ayşe Yılmaz",
    deleted: false,
  },
  target: {
    kind: AuditTargetResponseKind.User,
    id: "0198f0a2-0000-7000-8000-000000000007",
    name: "Khalid Al-Harbi",
    deleted: false,
  },
  ...overrides,
});

/** The details the API keeps for an operation (the union of AuditDetailsResponse). */
export const detailsOf: Partial<Record<AuditOperation, AuditDetailsResponse>> =
  {
    [AuditOperation.MemberInvited]: {
      operation: MemberInvitedDetailsResponseOperation.MemberInvited,
      role: role(BuiltInRole.Member),
      replacedInvitationId: null,
    },
    [AuditOperation.MemberRemoved]: {
      operation: MemberRemovedDetailsResponseOperation.MemberRemoved,
      role: role(BuiltInRole.Admin),
    },
    [AuditOperation.MemberRoleChanged]: {
      operation: MemberRoleChangedDetailsResponseOperation.MemberRoleChanged,
      oldRole: role(BuiltInRole.Member),
      newRole: role(BuiltInRole.Admin),
    },
    [AuditOperation.RoleCreated]: {
      operation: RoleCreatedDetailsResponseOperation.RoleCreated,
      permissions: [Permission.TenantAuditRead, Permission.TenantProfileRead],
    },
    [AuditOperation.RoleUpdated]: {
      operation: RoleUpdatedDetailsResponseOperation.RoleUpdated,
      oldName: "Billing lead",
      newName: "Billing lead",
      permissionsAdded: [
        Permission.TenantInvitationsCreate,
        Permission.TenantRolesRead,
      ],
      permissionsRemoved: [Permission.TenantAuditRead],
    },
    [AuditOperation.TenantOwnerInvited]: {
      operation: TenantOwnerInvitedDetailsResponseOperation.TenantOwnerInvited,
      invitationId: "0198f0a2-0000-7000-8000-0000000000a1",
      replacedInvitationId: null,
    },
    [AuditOperation.TenantPurgeWarned]: {
      operation: TenantPurgeWarnedDetailsResponseOperation.TenantPurgeWarned,
      // 9 Oct in UTC, 10 Oct in Dublin: the sentence names the day in the viewer's zone.
      purgesAt: "2026-10-09T23:30:00Z",
    },
    [AuditOperation.TenantRenamed]: {
      operation: TenantRenamedDetailsResponseOperation.TenantRenamed,
      oldName: "Northwind",
      newName: "Northwind Traders",
    },
    [AuditOperation.TenantCreated]: {
      operation: TenantCreatedDetailsResponseOperation.TenantCreated,
      plan: { id: "0198f0a2-0000-7000-8000-0000000000b2", name: "Pro" },
    },
    [AuditOperation.TenantPlanChanged]: {
      operation: TenantPlanChangedDetailsResponseOperation.TenantPlanChanged,
      oldPlan: { id: "0198f0a2-0000-7000-8000-0000000000b1", name: "Free" },
      newPlan: { id: "0198f0a2-0000-7000-8000-0000000000b2", name: "Pro" },
    },
    [AuditOperation.TenantEntitlementOverrideSet]: {
      operation:
        TenantEntitlementOverrideSetDetailsResponseOperation.TenantEntitlementOverrideSet,
      key: "members.max",
      oldValue: null,
      oldExpiresAt: null,
      newValue: { enabled: null, maximum: 25, unlimited: false },
      newExpiresAt: "2026-12-31T12:00:00Z",
      reason: null,
    },
    [AuditOperation.TenantEntitlementOverrideRemoved]: {
      operation:
        TenantEntitlementOverrideRemovedDetailsResponseOperation.TenantEntitlementOverrideRemoved,
      key: "roles.custom.max",
      oldValue: { enabled: null, maximum: 30, unlimited: false },
      oldExpiresAt: null,
    },
    [AuditOperation.TenantSuspended]: {
      operation: TenantSuspendedDetailsResponseOperation.TenantSuspended,
      reason: "Unpaid invoice 2026-114",
    },
  };

/**
 * ListAuditEntries that answers one page for each cursor: the first page without a cursor, the next for the cursor before it.
 * Each request's cursor is recorded.
 */
export function auditLogAnswer(
  pages: Record<string, CursorPageOfAuditEntryResponse>,
  cursors: (string | null)[] = [],
) {
  return getListAuditEntriesMockHandler(({ request }) => {
    const cursor = new URL(request.url).searchParams.get("cursor");
    cursors.push(cursor);
    return pages[cursor ?? ""] ?? { items: [], nextCursor: null };
  });
}
