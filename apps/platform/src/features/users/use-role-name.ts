"use client";

import { builtInRoleOf, type RoleResponse } from "@repo/api-client";
import { useTranslations } from "next-intl";

/** A role's text: a built-in role by its translated name, a custom role by the name the organisation stored. */
export function useRoleName() {
  const t = useTranslations("users.roles");
  return (role: Pick<RoleResponse, "name" | "builtIn">): string => {
    const builtIn = builtInRoleOf(role);
    return builtIn ? t(builtIn) : role.name;
  };
}
