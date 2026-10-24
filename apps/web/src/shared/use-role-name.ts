"use client";

import type { RoleDetailResponse, RoleResponse } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { builtInRoleOf } from "./roles";

/** The name of a role in the user's language: a built-in role from the messages, a custom role by its own name. */
export function useRoleName() {
  const t = useTranslations("shell.roles");
  return (role: Pick<RoleResponse, "name" | "builtIn">): string => {
    const builtIn = builtInRoleOf(role);
    return builtIn ? t(builtIn) : role.name;
  };
}

/** The line under a role in a role list: a built-in role's description, a custom role's permission count. */
export function useRoleDescription() {
  const t = useTranslations("members");
  return (role: RoleDetailResponse): string => {
    const builtIn = builtInRoleOf(role);
    return builtIn
      ? t(`roleDescriptions.${builtIn}`)
      : t("permissionCount", { count: role.permissions.length });
  };
}

/** The `role` of a message's `select`: the built-in role, which the text names with its grammar, or `other` for a custom role. */
export const roleSelect = (role: Pick<RoleResponse, "name" | "builtIn">) =>
  builtInRoleOf(role) ?? "other";
