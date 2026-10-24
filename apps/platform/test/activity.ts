import {
  type AuditActorResponse,
  AuditActorResponseKind,
  type AuditEntryResponse,
  AuditOperation,
  type AuditTargetResponse,
  AuditTargetResponseKind,
  type CursorPageOfAuditEntryResponse,
} from "@repo/api-client";
import { getListPlatformAuditEntriesMockHandler } from "@repo/api-client/msw";
import { northwind } from "./organisations";

// The platform log's entries of the Activity board (P6), and the answers of ListPlatformAuditEntries.

/** Ciara Byrne, the system admin who acts on the boards. */
export const ciara: AuditActorResponse = {
  kind: AuditActorResponseKind.User,
  userId: "0198f0a2-0000-7000-8000-0000000000c1",
  displayName: "Ciara Byrne",
  deleted: false,
};

/** A system admin who was deleted since: the API erased their names (A8). */
export const deletedActor: AuditActorResponse = {
  kind: AuditActorResponseKind.User,
  userId: "0198f0a2-0000-7000-8000-0000000000c2",
  displayName: null,
  deleted: true,
};

/** The platform itself: the purge and its warning have no person. */
export const thePlatform: AuditActorResponse = {
  kind: AuditActorResponseKind.Platform,
  userId: null,
  displayName: null,
  deleted: null,
};

/** An organisation as the target of an entry, with its name at that moment (K3). */
export const tenantTarget = (
  organisation: { id: string; name: string } = northwind,
): AuditTargetResponse => ({
  kind: AuditTargetResponseKind.Tenant,
  id: organisation.id,
  name: organisation.name,
  deleted: null,
});

/** John Byrne, a user the system admin acts on. */
export const john: AuditTargetResponse = {
  kind: AuditTargetResponseKind.User,
  id: "0198f0a2-0000-7000-8000-0000000000d1",
  name: "John Byrne",
  deleted: false,
};

/** Mehmet Kaya, a user who becomes a system admin and an Owner. */
export const mehmet: AuditTargetResponse = {
  kind: AuditTargetResponseKind.User,
  id: "0198f0a2-0000-7000-8000-0000000000d2",
  name: "Mehmet Kaya",
  deleted: false,
};

/** A user who was deleted: the target keeps the id, not the names. */
export const deletedUser: AuditTargetResponse = {
  kind: AuditTargetResponseKind.User,
  id: "0198f0a2-0000-7000-8000-0000000000d3",
  name: null,
  deleted: true,
};

/** An entry of the platform log: Ciara suspended John. */
export const entry = (
  overrides: Partial<AuditEntryResponse> = {},
): AuditEntryResponse => ({
  id: "0198f0a2-0000-7000-8000-0000000000e1",
  occurredAt: "2026-10-16T09:12:00Z",
  operation: AuditOperation.UserSuspended,
  details: null,
  actor: ciara,
  target: john,
  ...overrides,
});

/**
 * ListPlatformAuditEntries that answers one page for each cursor: the first page without a cursor, the next for the cursor
 * before it. Each request's address is recorded.
 */
export function auditLogAnswer(
  pages: Record<string, CursorPageOfAuditEntryResponse>,
  requests: URL[] = [],
) {
  return getListPlatformAuditEntriesMockHandler(({ request }) => {
    const url = new URL(request.url);
    requests.push(url);
    return (
      pages[url.searchParams.get("cursor") ?? ""] ?? {
        items: [],
        nextCursor: null,
      }
    );
  });
}
