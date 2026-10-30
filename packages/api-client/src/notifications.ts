import type {
  InfiniteData,
  QueryKey,
  UseInfiniteQueryOptions,
  UseQueryOptions,
} from "@tanstack/react-query";
import type {
  CursorPageOfNotificationResponse,
  UnreadNotificationCountResponse,
} from "./generated/endpoints.schemas.gen";
import {
  getGetMyUnreadNotificationCountQueryKey,
  getGetMyUnreadNotificationCountQueryOptions,
  getListMyNotificationsInfiniteQueryKey,
  getListMyNotificationsInfiniteQueryOptions,
  markAllMyNotificationsRead,
} from "./generated/me-notifications/me-notifications.gen";
import {
  getGetPlatformUnreadNotificationCountQueryKey,
  getGetPlatformUnreadNotificationCountQueryOptions,
  getListPlatformNotificationsInfiniteQueryKey,
  getListPlatformNotificationsInfiniteQueryOptions,
  markAllPlatformNotificationsRead,
} from "./generated/platform-notifications/platform-notifications.gen";
import {
  getGetUnreadNotificationCountQueryKey,
  getGetUnreadNotificationCountQueryOptions,
  getListNotificationsInfiniteQueryKey,
  getListNotificationsInfiniteQueryOptions,
  markAllNotificationsRead,
} from "./generated/tenant-notifications/tenant-notifications.gen";
import type { ProblemDetailsError } from "./mutator";

/**
 * The bell that one event of the live stream names (`apps/api/docs/flows/notifications.md`, section 4). The stream carries no
 * content: the client reads the bell's list and count again, through the reads that authorize them.
 */
export type BellSignal =
  | { scope: "tenant"; tenantId: string }
  | { scope: "personal" }
  | { scope: "platform" };

/** The bell of an event's data, or null for data that names no bell. */
export function bellOf(data: string): BellSignal | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(data);
  } catch {
    return null;
  }
  if (typeof parsed !== "object" || parsed === null) return null;
  const { scope, tenantId } = parsed as Record<string, unknown>;
  if (scope === "tenant" && typeof tenantId === "string")
    return { scope, tenantId };
  if (scope === "personal" || scope === "platform") return { scope };
  return null;
}

// The generated key leaves out an empty tenant, so it is the prefix of every tenant's key.
const everyTenant = "";

/**
 * The query keys a signal changes, by their generated keys (F2). A personal notification shows in every tenant bell too, so a
 * personal signal names them all.
 */
export function bellKeysOf(signal: BellSignal): QueryKey[] {
  switch (signal.scope) {
    case "tenant":
      return [
        getListNotificationsInfiniteQueryKey(signal.tenantId),
        getGetUnreadNotificationCountQueryKey(signal.tenantId),
      ];
    case "personal":
      return [
        getListMyNotificationsInfiniteQueryKey(),
        getGetMyUnreadNotificationCountQueryKey(),
        getListNotificationsInfiniteQueryKey(everyTenant),
        getGetUnreadNotificationCountQueryKey(everyTenant),
      ];
    case "platform":
      return [
        getListPlatformNotificationsInfiniteQueryKey(),
        getGetPlatformUnreadNotificationCountQueryKey(),
      ];
  }
}

/** Every bell's keys: what a tab reads again when its stream opens again, it gets focus or it comes back online. */
export const everyBellKey: QueryKey[] = [
  ...bellKeysOf({ scope: "personal" }),
  ...bellKeysOf({ scope: "platform" }),
];

type Page = CursorPageOfNotificationResponse;
type Cursor = string | undefined;

/**
 * One bell's reads and its one command (`apps/api/docs/flows/notifications.md`, section 3): the list by the API's cursor, newest
 * first, the unread count, Mark all as read, and the keys that the command invalidates (F47).
 */
export type Bell = {
  list: UseInfiniteQueryOptions<
    Page,
    ProblemDetailsError,
    InfiniteData<Page, Cursor>,
    QueryKey,
    Cursor
  >;
  count: UseQueryOptions<
    UnreadNotificationCountResponse,
    ProblemDetailsError,
    UnreadNotificationCountResponse,
    QueryKey
  >;
  markAll: () => Promise<void>;
  keys: QueryKey[];
};

const paging = {
  initialPageParam: undefined,
  getNextPageParam: (last: Page) => last.nextCursor ?? undefined,
};

/** The tenant web's bell inside an organisation: that tenant's notifications and the personal ones. */
export const tenantBell = (tenantId: string): Bell => ({
  list: getListNotificationsInfiniteQueryOptions(tenantId, undefined, {
    query: paging,
  }),
  count: getGetUnreadNotificationCountQueryOptions(tenantId),
  markAll: () => markAllNotificationsRead(tenantId),
  keys: bellKeysOf({ scope: "tenant", tenantId }),
});

/** The personal bell, on the pages without an organisation: the personal notifications only. */
export const personalBell = (): Bell => ({
  list: getListMyNotificationsInfiniteQueryOptions(undefined, {
    query: paging,
  }),
  count: getGetMyUnreadNotificationCountQueryOptions(),
  markAll: () => markAllMyNotificationsRead(),
  keys: bellKeysOf({ scope: "personal" }).slice(0, 2),
});

/** The platform's bell. */
export const platformBell = (): Bell => ({
  list: getListPlatformNotificationsInfiniteQueryOptions(undefined, {
    query: paging,
  }),
  count: getGetPlatformUnreadNotificationCountQueryOptions(),
  markAll: () => markAllPlatformNotificationsRead(),
  keys: bellKeysOf({ scope: "platform" }),
});
