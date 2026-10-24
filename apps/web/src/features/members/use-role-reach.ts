"use client";

import { Permission } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { useCanActOn, useTenantRoles } from "@/shared/use-can-act-on";

/**
 * Why the viewer cannot act on a role, or null when they can (F51). A role that holds `tenant.owners.manage`, the Owner role, gets
 * the K1 text the caller gives; any other role the escalation text. A role that ListRoles no longer has says so. While the roles
 * load, the answer is an empty reason: the item waits, disabled.
 */
export function useRoleReach(): (
  roleId: string,
  ownerReason: string,
) => string | null {
  const t = useTranslations("members");
  const canActOn = useCanActOn();
  const roles = useTenantRoles();
  return (roleId, ownerReason) => {
    if (canActOn(roleId)) return null;
    if (!roles.data) return "";
    const role = roles.data.items.find((item) => item.id === roleId);
    if (!role) return t("roleGone");
    return role.permissions.includes(Permission.TenantOwnersManage)
      ? ownerReason
      : t("roleOutOfReach");
  };
}
