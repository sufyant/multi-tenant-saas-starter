"use client";

import { Permission } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { PermissionDenied } from "@/shared/permission-denied";
import { useCan } from "@/shared/tenant-context";
import { RoleEditor } from "./role-editor";
import { RolesFrame } from "./roles-frame";

/** A new role (the RoleCreate board), for a viewer with `tenant.roles.create` (F5). */
export function NewRolePage() {
  const t = useTranslations();
  const can = useCan();
  return (
    <RolesFrame>
      {can(Permission.TenantRolesCreate) ? (
        <RoleEditor />
      ) : (
        <PermissionDenied page={t("roles.newRole")} />
      )}
    </RolesFrame>
  );
}
