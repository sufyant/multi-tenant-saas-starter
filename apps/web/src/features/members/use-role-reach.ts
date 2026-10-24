"use client";

import { Permission } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { useCan } from "@/shared/tenant-context";
import { useCanActOn, useTenantRoles } from "@/shared/use-can-act-on";

/** Why the viewer cannot act on a role, or null when they can (F51). The caller gives the Owner role's K1 text. */
export function useRoleReach(): (
  roleId: string,
  ownerReason: string,
) => string | null {
  const t = useTranslations("members");
  const can = useCan();
  const canActOn = useCanActOn();
  const roles = useTenantRoles();
  return (roleId, ownerReason) => {
    if (canActOn(roleId)) return null;
    if (!can(Permission.TenantRolesRead)) return t("rolesUnreadable");
    // The roles still load: the item waits, disabled.
    if (!roles.data) return "";
    const role = roles.data.items.find((item) => item.id === roleId);
    if (!role) return t("roleGone");
    return role.permissions.includes(Permission.TenantOwnersManage)
      ? ownerReason
      : t("roleOutOfReach");
  };
}
