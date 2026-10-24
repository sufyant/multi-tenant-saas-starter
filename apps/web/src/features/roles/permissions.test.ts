import { Permission } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import {
  lockOf,
  needRolesRead,
  permissionGroups,
  withRequired,
} from "./permissions";

describe("permissionGroups", () => {
  it("PermissionGroups_TenantPool_HoldEveryTenantPermissionOnce", () => {
    const grouped = permissionGroups.flatMap(
      (group): readonly Permission[] => group.permissions,
    );

    expect([...grouped].sort()).toEqual(
      Object.values(Permission)
        .filter((name) => name.startsWith("tenant."))
        .sort(),
    );
  });
});

describe("lockOf", () => {
  it("LockOf_ProfileRead_LockedForEveryRole", () => {
    expect(lockOf(Permission.TenantProfileRead, [])).toBe("everyRole");
  });

  it.each(needRolesRead)("LockOf_RolesReadWith_%s_Locked", (permission) => {
    expect(lockOf(Permission.TenantRolesRead, [permission])).toBe(
      "rolesReadNeeded",
    );
  });

  it("LockOf_RolesReadWithoutTheFour_Unlocked", () => {
    expect(
      lockOf(Permission.TenantRolesRead, [
        Permission.TenantRolesRead,
        Permission.TenantMembersRead,
        Permission.TenantInvitationsRead,
      ]),
    ).toBeNull();
  });
});

describe("withRequired", () => {
  it("WithRequired_InviteChecked_AddsProfileAndRolesReadInTheScreensOrder", () => {
    expect(
      withRequired([
        Permission.TenantInvitationsCreate,
        Permission.TenantMembersRead,
      ]),
    ).toEqual([
      Permission.TenantProfileRead,
      Permission.TenantMembersRead,
      Permission.TenantInvitationsCreate,
      Permission.TenantRolesRead,
    ]);
  });
});
