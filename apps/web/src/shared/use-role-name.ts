"use client";

import {
  builtInRoleOf,
  type RoleDetailResponse,
  type RoleResponse,
} from "@repo/api-client";
import { useTranslations } from "next-intl";
import { roleNameOf } from "./roles";

/** The name of a role in the user's language: a built-in role from the messages, a custom role by its own name. */
export function useRoleName() {
  const t = useTranslations("shell.roleNames");
  return (role: Pick<RoleResponse, "name" | "builtIn">): string =>
    roleNameOf(role, (builtIn) => t(builtIn));
}

/** The line under a role in a role list: a built-in role's description, "Custom role" and the permission count of a custom one. */
export function useRoleDescription() {
  const t = useTranslations("members");
  return (role: RoleDetailResponse): string => {
    const builtIn = builtInRoleOf(role);
    return builtIn
      ? t(`roleDescriptions.${builtIn}`)
      : t("customRolePermissions", { count: role.permissions.length });
  };
}

/**
 * A built-in role's own note under `notes`, or null: the Billing manager's words that it is for billing only and takes no seat
 * (14.01, 14.03). A note is a role's text, like its name; it decides nothing (F51).
 */
export function useRoleNote() {
  const t = useTranslations();
  return (
    role: Pick<RoleResponse, "name" | "builtIn">,
    notes: "roles.builtInNotes" | "roles.seatNotes" | "members.inviteNotes",
  ): string | null => {
    const builtIn = builtInRoleOf(role);
    const key = `${notes}.${builtIn}` as "roles.seatNotes.billing_manager";
    return builtIn && t.has(key) ? t(key) : null;
  };
}

/** The `role` of a message's `select`: the built-in role, which the text names with its grammar, or `other` for a custom role. */
export const roleSelect = (role: Pick<RoleResponse, "name" | "builtIn">) =>
  builtInRoleOf(role) ?? "other";
