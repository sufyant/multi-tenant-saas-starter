"use client";

import { Permission, problemOf } from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { ListError } from "@repo/ui/components/list-error";
import { Skeleton } from "@repo/ui/components/skeleton";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { PermissionDenied } from "@/shared/permission-denied";
import { leaveOf } from "@/shared/problem-screen";
import { useCan, useTenant } from "@/shared/tenant-context";
import { holdsAll, useTenantRoles } from "@/shared/use-can-act-on";
import { useRoleName } from "@/shared/use-role-name";
import { PermissionList } from "./permission-list";
import { RoleEditor } from "./role-editor";
import { RolesFrame } from "./roles-frame";

/**
 * One role (the RoleEdit and RoleBuiltIn boards), read from ListRoles: the API has no GET for one role. A built-in role is
 * read-only, with the board's banner and every permission it holds, Manage Owners included for the Owner. A custom role opens the
 * editor for a viewer with `tenant.roles.update` whose role holds every permission of it (F51); anyone else reads it.
 */
export function RolePage({ roleId }: { roleId: string }) {
  const t = useTranslations();
  const roleName = useRoleName();
  const can = useCan();
  const { tenant, detail } = useTenant();
  const roles = useTenantRoles();

  if (!can(Permission.TenantRolesRead))
    return (
      <RolesFrame>
        <PermissionDenied page={t("settings.roles")} />
      </RolesFrame>
    );
  if (roles.isPending)
    return (
      <RolesFrame>
        <Skeleton rows={6} label={t("roles.loading")} />
      </RolesFrame>
    );
  if (roles.isError)
    return (
      <RolesFrame>
        <ListError
          title={t("roles.loadError")}
          problem={problemOf(roles.error)}
          leave={leaveOf(roles.error)}
          at={roles.errorUpdatedAt}
          onRetry={() => void roles.refetch()}
        />
      </RolesFrame>
    );

  const role = roles.data.items.find((item) => item.id === roleId);
  if (!role)
    return (
      <RolesFrame>
        <EmptyState
          kind="error"
          title={t("roles.gone")}
          action={
            <Button
              render={<Link href={`/${tenant.slug}/settings/roles`} />}
              nativeButton={false}
            >
              {t("roles.backToRoles")}
            </Button>
          }
        />
      </RolesFrame>
    );
  if (
    !role.builtIn &&
    can(Permission.TenantRolesUpdate) &&
    holdsAll(detail.permissions, role)
  )
    return (
      <RolesFrame>
        <RoleEditor key={role.id} role={role} />
      </RolesFrame>
    );
  return (
    <RolesFrame>
      <h1 className="text-page-title">
        <bdi>{roleName(role)}</bdi>
      </h1>
      {role.builtIn && (
        <Banner tone="info" title={t("roles.builtInTitle")}>
          {t("roles.builtInText")}
        </Banner>
      )}
      <div className="max-w-3xl">
        <PermissionList held={role.permissions} />
      </div>
    </RolesFrame>
  );
}
