"use client";

import { ListUsersSort, UserResponseStatus } from "@repo/api-client";
import {
  parseAsInteger,
  parseAsString,
  parseAsStringLiteral,
  useQueryStates,
} from "nuqs";

/**
 * The status tabs (the Users board): All, Active and Suspended. A deleted user shows under All. The list never sends
 * `isSystemAdmin`: without it the API lists every user, and an empty value answers 400; the System admins page filters by it.
 */
export const tabs = [
  UserResponseStatus.Active,
  UserResponseStatus.Suspended,
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
  sort: parseAsStringLiteral(Object.values(ListUsersSort)),
};

export function useListParams() {
  return useQueryStates(parsers, { history: "push" });
}
