import { describe, expect, it } from "vitest";
import {
  getGetMyUnreadNotificationCountQueryKey,
  getGetPlatformUnreadNotificationCountQueryKey,
  getGetUnreadNotificationCountQueryKey,
  getListMyNotificationsInfiniteQueryKey,
  getListNotificationsInfiniteQueryKey,
  getListPlatformNotificationsInfiniteQueryKey,
} from "./generated/index.gen";
import { bellKeysOf, bellOf, everyBellKey } from "./notifications";

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
