"use client";

import {
  ListTenantsSort,
  type TenantResponseStatus as Status,
  TenantResponseStatus,
  type TenantStatusCountsResponse,
} from "@repo/api-client";
import { parseAsString, parseAsStringLiteral, useQueryStates } from "nuqs";

/**
 * The status tabs (4.01): All, then one tab for each status a person looks for. All holds every status but deleted, and Closed holds
 * the organisations being deleted too, because being deleted lasts minutes (the maintainer's decision of 2026-11-08).
 */
export const tabs = [
  TenantResponseStatus.Active,
  TenantResponseStatus.Suspended,
  TenantResponseStatus.Closed,
  TenantResponseStatus.Purged,
] as const;
export type Tab = (typeof tabs)[number];

/** The statuses a tab asks `ListTenants` for; null is All. */
export const statusesOf = (tab: Tab | null): Status[] => {
  switch (tab) {
    case null:
      return [
        TenantResponseStatus.Active,
        TenantResponseStatus.Suspended,
        TenantResponseStatus.Closed,
        TenantResponseStatus.Purging,
      ];
    case TenantResponseStatus.Closed:
      return [TenantResponseStatus.Closed, TenantResponseStatus.Purging];
    default:
      return [tab];
  }
};

/** A tab's count: the sum of its statuses' counts, which follow the search and ignore the status (`ListTenants.statusCounts`). */
export const countOf = (counts: TenantStatusCountsResponse, tab: Tab | null) =>
  statusesOf(tab).reduce((sum, status) => sum + counts[status], 0);

/**
 * The list's state in the URL (F8, F9): `?status=…&q=…&sort=…`, never the page; the list grows with Load more. Each change pushes a
 * history entry, so back and forward restore it. A value the contract does not have reads as absent.
 */
const parsers = {
  status: parseAsStringLiteral(tabs),
  q: parseAsString.withDefault(""),
  sort: parseAsStringLiteral(Object.values(ListTenantsSort)),
};

export function useListParams() {
  return useQueryStates(parsers, { history: "push" });
}
