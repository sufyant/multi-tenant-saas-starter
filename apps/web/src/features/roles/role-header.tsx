"use client";

import type { RoleDetailResponse } from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { ArrowLeftIcon, Icon } from "@repo/ui/components/icon";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useTenant } from "@/shared/tenant-context";
import { useRoleUsers } from "./role-users";

/**
 * The head of a role's page (9.08, 9.10, 9.11, 9.20): "Back to Roles", the title, and under an existing role its summary: the
 * type, the permissions and who uses it, as the list's columns count them.
 */
export function RoleHeader({
  title,
  role,
}: {
  title: string;
  role?: RoleDetailResponse;
}) {
  const t = useTranslations();
  const { tenant } = useTenant();
  const usersOf = useRoleUsers();
  return (
    <div className="flex flex-col gap-3">
      <div>
        <Button
          variant="ghost"
          icon={
            <Icon icon={ArrowLeftIcon} size={16} className="rtl:rotate-180" />
          }
          render={<Link href={`/${tenant.slug}/settings/roles`} />}
          nativeButton={false}
        >
          {t("roles.backToRoles")}
        </Button>
      </div>
      <div className="flex flex-col gap-0.5">
        <h1 className="text-page-title">
          <bdi>{title}</bdi>
        </h1>
        {role && (
          <p className="text-body text-muted-foreground">
            {t("roles.summary", {
              type: role.builtIn ? t("roles.builtIn") : t("roles.custom"),
              permissions: t("members.permissionCount", {
                count: role.permissions.length,
              }),
              users: usersOf(role, "usageBoth") ?? t("roles.notInUseLower"),
            })}
          </p>
        )}
      </div>
    </div>
  );
}
