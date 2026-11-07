"use client";

import type { RoleDetailResponse } from "@repo/api-client";
import { useTranslations } from "next-intl";

/**
 * Who uses a role, as `role.in_use` counts them: its members and its pending invitations, an expired one included. `both` joins
 * the two: "2 members · 1 pending invitation" in a list, "2 members and 1 pending invitation" in a sentence. Nobody: null.
 */
export function useRoleUsers() {
  const t = useTranslations("roles");
  return (
    role: RoleDetailResponse,
    both: "usageBoth" | "inUseBoth",
  ): string | null => {
    const people =
      role.memberCount > 0 && t("usageMembers", { count: role.memberCount });
    const invitations =
      role.pendingInvitationCount > 0 &&
      t("usageInvitations", { count: role.pendingInvitationCount });
    if (people && invitations) return t(both, { members: people, invitations });
    return people || invitations || null;
  };
}
