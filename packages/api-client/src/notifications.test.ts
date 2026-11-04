import { describe, expect, it } from "vitest";
import {
  getGetMyUnreadNotificationCountQueryKey,
  getGetPlatformUnreadNotificationCountQueryKey,
  getGetUnreadNotificationCountQueryKey,
  getListMyNotificationsInfiniteQueryKey,
  getListNotificationsInfiniteQueryKey,
  getListPlatformNotificationsInfiniteQueryKey,
} from "./generated/index.gen";
import { getGetMyTenantQueryKey } from "./generated/tenant-tenants/tenant-tenants.gen";
import {
  accessKeysOf,
  bellKeysOf,
  bellOf,
  everyBellKey,
  personalBell,
  platformBell,
  tenantBell,
} from "./notifications";

const tenantId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";

describe("bellOf", () => {
  it.each([
    [
      `{"scope":"tenant","tenantId":"${tenantId}"}`,
      { scope: "tenant", tenantId },
    ],
    [`{"scope":"personal"}`, { scope: "personal" }],
    [`{"scope":"platform"}`, { scope: "platform" }],
  ])("BellOf_%s_NamesTheBell", (data, bell) => {
    expect(bellOf(data)).toEqual(bell);
  });

  it.each([
    ["not json"],
    [`{"scope":"tenant"}`],
    [`{"scope":"other"}`],
    ["null"],
  ])("BellOf_%s_IsNull", (data) => {
    expect(bellOf(data)).toBeNull();
  });
});

describe("bellKeysOf", () => {
  it("BellKeysOf_Tenant_NamesThatTenantsListAndCount", () => {
    expect(bellKeysOf({ scope: "tenant", tenantId })).toEqual([
      getListNotificationsInfiniteQueryKey(tenantId),
      getGetUnreadNotificationCountQueryKey(tenantId),
    ]);
  });

  it("BellKeysOf_Personal_NamesThePersonalBellAndEveryTenantBell", () => {
    expect(bellKeysOf({ scope: "personal" })).toEqual([
      getListMyNotificationsInfiniteQueryKey(),
      getGetMyUnreadNotificationCountQueryKey(),
      ["infinite", "listNotifications"],
      ["getUnreadNotificationCount"],
    ]);
  });

  it("BellKeysOf_Platform_NamesThePlatformsListAndCount", () => {
    expect(bellKeysOf({ scope: "platform" })).toEqual([
      getListPlatformNotificationsInfiniteQueryKey(),
      getGetPlatformUnreadNotificationCountQueryKey(),
    ]);
  });

  it("EveryBellKey_NamesTheThreeBells", () => {
    expect(everyBellKey).toEqual([
      ...bellKeysOf({ scope: "personal" }),
      ...bellKeysOf({ scope: "platform" }),
    ]);
  });
});

// A notification may say the member's role or the organisation changed (member.role_changed, tenant.suspended), and the stream
// names no type: a signal reads the organisation again, so the shell shows the change at once (F59).
describe("accessKeysOf", () => {
  it("AccessKeysOf_TenantSignal_ReadsThatOrganisationAgain", () => {
    expect(accessKeysOf({ scope: "tenant", tenantId })).toEqual([
      getGetMyTenantQueryKey(tenantId),
    ]);
  });

  it("AccessKeysOf_PersonalSignal_ReadsEveryOrganisationAgain", () => {
    expect(accessKeysOf({ scope: "personal" })).toEqual([
      getGetMyTenantQueryKey(""),
    ]);
  });

  it("AccessKeysOf_PlatformSignal_ReadsNoOrganisation", () => {
    expect(accessKeysOf({ scope: "platform" })).toEqual([]);
  });
});

// The Unread tab reads the list with the filter unread (the API's K5). Its key starts with the bell's list key, which the bell's
// keys name, so a signal and Mark all as read read it again too (F57).
describe("unreadList", () => {
  it.each([
    ["tenant", tenantBell(tenantId)],
    ["personal", personalBell()],
    ["platform", platformBell()],
  ])("UnreadList_%sBell_FiltersTheUnreadOnes", (_, bell) => {
    expect(bell.unreadList.queryKey).toEqual([
      ...bell.list.queryKey,
      { unread: true },
    ]);
    expect(bell.keys).toContainEqual(bell.list.queryKey);
  });
});
