"use client";

import type { RoleResponse } from "@repo/api-client";
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
