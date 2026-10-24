"use client";

import {
  getGetMeQueryOptions,
  Permission,
  type RoleDetailResponse,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Freshness } from "@repo/ui/components/freshness";
import { Icon, LockIcon } from "@repo/ui/components/icon";
import { RowMenu, type RowMenuItem } from "@repo/ui/components/row-menu";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { Tag } from "@repo/ui/components/tag";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { ListError } from "@/shared/list-error";
import { PermissionDenied } from "@/shared/permission-denied";
import { inListOrder } from "@/shared/roles";
import { useCan, useTenant } from "@/shared/tenant-context";
import { holdsAll, useTenantRoles } from "@/shared/use-can-act-on";
import { useRoleName } from "@/shared/use-role-name";
import { DeleteRoleDialog } from "./delete-role-dialog";
import { RolesFrame } from "./roles-frame";

/**
 * Settings > Roles (the RolesList, RolesAdmin and RolesEmpty boards): Name, Permissions and Type, the built-in roles first (F51).
 * A built-in role offers "View permissions" only. A custom role offers "Edit role" and "Delete role" by their permissions (F5),
 * both disabled with the reason when the viewer's role does not hold every permission of it (F51); without the update permission
 * it opens read-only.
 */
export function RolesPage() {
  const t = useTranslations();
  const roleName = useRoleName();
  const can = useCan();
  const router = useRouter();
  const { tenant, detail } = useTenant();
  const roles = useTenantRoles();
  const me = useQuery(getGetMeQueryOptions());
  const [deleting, setDeleting] = useState<RoleDetailResponse | null>(null);

  if (!can(Permission.TenantRolesRead))
    return (
      <RolesFrame>
        <PermissionDenied page={t("settings.roles")} />
      </RolesFrame>
    );

  const base = `/${tenant.slug}/settings/roles`;
  const canCreate = can(Permission.TenantRolesCreate);
  const canUpdate = can(Permission.TenantRolesUpdate);
  const canDelete = can(Permission.TenantRolesDelete);
  const open = (role: RoleDetailResponse) => router.push(`${base}/${role.id}`);
  const createButton = canCreate && (
    <Button
      variant="primary"
      render={<Link href={`${base}/new`} />}
      nativeButton={false}
    >
      {t("roles.create")}
    </Button>
  );

  const name = (role: RoleDetailResponse) => (
    <span className="flex min-w-0 items-center gap-1.5 font-medium">
      {role.builtIn && (
        <Icon icon={LockIcon} size={14} className="text-muted-foreground" />
      )}
      <bdi className="truncate">{roleName(role)}</bdi>
    </span>
  );
  const columns: TableColumn<RoleDetailResponse>[] = [
    { key: "name", header: t("roles.name"), render: name },
    {
      key: "permissions",
      header: t("roles.permissions"),
      secondary: true,
      render: (role) =>
        t("members.permissionCount", { count: role.permissions.length }),
    },
    {
      key: "type",
      header: t("roles.type"),
      card: "end",
      render: (role) => role.builtIn && <Tag>{t("roles.builtIn")}</Tag>,
    },
  ];

  const itemsFor = (role: RoleDetailResponse): RowMenuItem[] => {
    const view = {
      label: t("roles.viewPermissions"),
      onSelect: () => open(role),
    };
    if (role.builtIn) return [view];
    const reason = holdsAll(detail.permissions, role)
      ? undefined
      : t("roles.outOfReach");
    const items: RowMenuItem[] = [];
    if (!canUpdate || reason) items.push(view);
    if (canUpdate)
      items.push({
        label: t("roles.edit"),
        onSelect: () => open(role),
        disabled: reason !== undefined,
        reason,
      });
    if (canDelete)
      items.push(
        { separator: true },
        {
          label: t("roles.delete"),
          tone: "destructive",
          onSelect: () => setDeleting(role),
          disabled: reason !== undefined,
          reason,
        },
      );
    return items;
  };

  const body = (() => {
    if (roles.isPending)
      return <Skeleton rows={4} label={t("roles.loading")} />;
    if (roles.isError)
      return (
        <ListError
          title={t("roles.loadError")}
          error={roles.error}
          at={roles.errorUpdatedAt}
          onRetry={() => void roles.refetch()}
        />
      );
    const rows = inListOrder(roles.data.items);
    return (
      <>
        <Table
          columns={columns}
          rows={rows}
          rowKey="id"
          label={t("roles.title")}
          actions={(role) => (
            <RowMenu
              label={t("roles.actionsFor", { name: roleName(role) })}
              items={itemsFor(role)}
            />
          )}
          actionsLabel={t("roles.actions")}
          rowCount={roles.data.total}
          page={roles.data.page}
          pageSize={roles.data.pageSize}
          pager={{
            previous: t("common.previous"),
            next: t("common.next"),
            page: (current, count) =>
              t("common.page", { page: current, count }),
          }}
        />
        {rows.every((role) => role.builtIn) && (
          <EmptyState
            title={t("roles.noCustom")}
            description={t("roles.noCustomText")}
            action={createButton}
          />
        )}
      </>
    );
  })();

  return (
    <RolesFrame>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-page-title">{t("roles.title")}</h1>
          {roles.dataUpdatedAt > 0 && (
            <Freshness
              at={new Date(roles.dataUpdatedAt)}
              locale={t("locale.format")}
              timeZone={me.data?.timeZone}
              label={t("common.updated")}
            />
          )}
        </div>
        {createButton}
      </div>
      <p className="max-w-prose text-muted-foreground">{t("roles.lead")}</p>
      {body}
      {deleting && (
        <DeleteRoleDialog role={deleting} onClose={() => setDeleting(null)} />
      )}
    </RolesFrame>
  );
}
