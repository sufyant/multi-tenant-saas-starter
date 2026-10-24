"use client";

import { ListTenantsSort, TenantResponseStatus } from "@repo/api-client";
import {
  parseAsInteger,
  parseAsString,
  parseAsStringLiteral,
  useQueryStates,
} from "nuqs";

/**
 * The status tabs (the Main board): All, then one tab for each status a person looks for. Being deleted lasts minutes, so it has
 * no tab; it shows under All. The API's `status` filter takes one status, so the Closed tab cannot hold it too.
 */
export const tabs = [
  TenantResponseStatus.Active,
  TenantResponseStatus.Suspended,
  TenantResponseStatus.Closed,
  TenantResponseStatus.Purged,
] as const;
export type Tab = (typeof tabs)[number];

/**
 * The list's state in the URL (F8, F9): `?status=…&q=…&page=…&sort=…`. Each change pushes a history entry, so back and forward
 * restore it. A value the contract does not have reads as absent.
 */
const parsers = {
  status: parseAsStringLiteral(tabs),
  q: parseAsString.withDefault(""),
  page: parseAsInteger.withDefault(1),
  sort: parseAsStringLiteral(Object.values(ListTenantsSort)),
};

export function useListParams() {
  return useQueryStates(parsers, { history: "push" });
}
