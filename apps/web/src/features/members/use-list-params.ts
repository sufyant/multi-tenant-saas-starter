"use client";

import {
  ListInvitationsSort,
  ListMembersSort,
  MemberRole,
} from "@repo/api-client";
import {
  parseAsInteger,
  parseAsString,
  parseAsStringLiteral,
  useQueryStates,
} from "nuqs";

export const tabs = ["members", "invitations"] as const;
export type Tab = (typeof tabs)[number];

/** Search waits this long after the last key, so a word sends one request, not one per letter. */
export const searchDelay = 300;

/**
 * The page's list state in the URL (F8, F9): `?tab=invitations&q=…&role=…&page=…&sort=…`. Each change pushes a history
 * entry, so back and forward restore it. A value the contract does not have reads as absent.
 */
const sorts = [
  ...Object.values(ListMembersSort),
  ...Object.values(ListInvitationsSort),
];

/** A sort value of the contract, or null. */
export const sortValue = (value: string | null) =>
  sorts.find((sort) => sort === value) ?? null;

const parsers = {
  tab: parseAsStringLiteral(tabs).withDefault("members"),
  q: parseAsString.withDefault(""),
  role: parseAsStringLiteral(Object.values(MemberRole)),
  page: parseAsInteger.withDefault(1),
  sort: parseAsStringLiteral(sorts),
};

export function useListParams() {
  return useQueryStates(parsers, { history: "push" });
}

export type ListParams = ReturnType<typeof useListParams>[0];
