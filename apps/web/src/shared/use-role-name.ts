"use client";

import {
  builtInRoleOf,
  type RoleDetailResponse,
  type RoleResponse,
} from "@repo/api-client";
import { useTranslations } from "next-intl";
import { roleNameOf } from "./roles";
import { useTenant } from "./tenant-context";

/** The name of a role in the user's language: a built-in role from the messages, a custom role by its own name. */
export function useRoleName() {
  const t = useTranslations("shell.roleNames");
  return (role: Pick<RoleResponse, "name" | "builtIn">): string =>
    roleNameOf(role, (builtIn) => t(builtIn));
}

/**
 * The texts of a built-in role that have an "off" form, the Billing manager's, which name Billing and Usage: while the deployment's
 * billing is off (GetMyTenant.billingEnabled) the role sees the plan and its limits only (14.01, 14.03, 14.06).
 */
function useOff() {
  const { detail } = useTenant();
  return detail.billingEnabled ? "" : "Off";
}

/** The line under a role in a role list: a built-in role's description, "Custom role" and the permission count of a custom one. */
export function useRoleDescription() {
  const t = useTranslations("members");
  const off = useOff();
  return (role: RoleDetailResponse): string => {
    const builtIn = builtInRoleOf(role);
    if (!builtIn)
      return t("customRolePermissions", { count: role.permissions.length });
    const key = `roleDescriptions${off}.${builtIn}` as "roleDescriptions.owner";
    return t.has(key) ? t(key) : t(`roleDescriptions.${builtIn}`);
  };
}

/**
 * A built-in role's own note under `notes`, or null: the Billing manager's words that it is for billing only and takes no seat
 * (14.01, 14.03), in their "off" form while billing is off. A note is a role's text, like its name; it decides nothing (F51).
 */
export function useRoleNote() {
  const t = useTranslations();
  const off = useOff();
  return (
    role: Pick<RoleResponse, "name" | "builtIn">,
    notes: "roles.builtInNotes" | "roles.seatNotes" | "members.inviteNotes",
  ): string | null => {
    const builtIn = builtInRoleOf(role);
    if (!builtIn) return null;
    const offKey =
      `${notes}${off}.${builtIn}` as "roles.seatNotes.billing_manager";
    const key = `${notes}.${builtIn}` as "roles.seatNotes.billing_manager";
    if (t.has(offKey)) return t(offKey);
    return t.has(key) ? t(key) : null;
  };
}

/** The `role` of a message's `select`: the built-in role, which the text names with its grammar, or `other` for a custom role. */
export const roleSelect = (role: Pick<RoleResponse, "name" | "builtIn">) =>
  builtInRoleOf(role) ?? "other";
