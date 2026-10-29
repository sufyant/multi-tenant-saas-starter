"use client";

import {
  FeatureKey,
  getGetMeQueryOptions,
  LimitKey,
  Permission,
  problemOf,
  type RoleDetailResponse,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Freshness } from "@repo/ui/components/freshness";
import { Icon, LockIcon } from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { RowMenu, type RowMenuItem } from "@repo/ui/components/row-menu";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { Tag } from "@repo/ui/components/tag";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useId, useState } from "react";
import { PermissionDenied } from "@/shared/permission-denied";
import { usePlan } from "@/shared/plan";
import { PlanBanner, reasonIdsOf } from "@/shared/plan-banner";
import { leaveOf } from "@/shared/problem-screen";
import { inListOrder } from "@/shared/roles";
import { useCan, useTenant } from "@/shared/tenant-context";
import { holdsAll, useTenantRoles } from "@/shared/use-can-act-on";
import { sentenceOf, usePlanReason } from "@/shared/use-plan-reason";
import { useRoleName } from "@/shared/use-role-name";
import { DeleteRoleDialog } from "./delete-role-dialog";
import { RolesFrame } from "./roles-frame";

/**
 * Settings > Roles (the RolesList, RolesAdmin, RolesLimit and RolesEmpty boards): Name, Permissions, Used by and Type, the built-in
 * roles first (F51). A built-in role offers "View permissions". A custom role offers "Edit role" and "Delete role" by their
 * permissions (F5), both disabled with the reason when the viewer's role does not hold every permission of it (F51); without the
 * update permission it opens read-only. A role in use offers its members, or its invitations when only invitations use it, and
 * its "Delete role" is disabled with the counts, as `role.in_use` counts them. The plan (the plan-limit boards, through `usePlan`):
 * a viewer who may create sees the count of `roles.custom.max` (none when unlimited); at the limit "Create role" is disabled with
 * the reason, over it with the banner. With `roles.custom` off the custom roles stay listed, "Create role" and "Edit role" are
 * disabled with the feature's reason in place of the count, and "Delete role" keeps its own rules.
 */
export function RolesPage() {
  const t = useTranslations();
  const roleName = useRoleName();
  const can = useCan();
  const router = useRouter();
  const { tenant, detail } = useTenant();
  const roles = useTenantRoles();
  const me = useQuery(getGetMeQueryOptions());
  const plan = usePlan();
  const planReason = usePlanReason();
  const [deleting, setDeleting] = useState<RoleDetailResponse | null>(null);
  const counterId = useId();
  const bannerId = useId();

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
  const members = `/${tenant.slug}/settings/members`;

  // The plan closes creating and editing a custom role; deleting stays (E4, E5). The feature's reason wins over the limit's.
  const featureOff = plan.on(FeatureKey.RolesCustom)
    ? undefined
    : planReason.feature(FeatureKey.RolesCustom);
  const roleLimit = plan.limit(LimitKey.RolesCustomMax);
  const over = featureOff === undefined && roleLimit.overLimit;
  const counter = (() => {
    if (featureOff) return featureOff;
    if (roleLimit.limit === null) return null;
    if (roleLimit.atLimit)
      return sentenceOf(planReason.limit(LimitKey.RolesCustomMax));
    return t("plans.rolesCount", {
      count: roleLimit.count,
      limit: roleLimit.limit,
    });
  })();
  // The button waits for the roles, so it shows with the list it acts on.
  const createButton =
    canCreate &&
    roles.data !== undefined &&
    (featureOff || roleLimit.full ? (
      <Button
        variant="primary"
        disabled
        aria-describedby={over ? reasonIdsOf(bannerId) : counterId}
      >
        {t("roles.create")}
      </Button>
    ) : (
      <Button
        variant="primary"
        render={<Link href={`${base}/new`} />}
        nativeButton={false}
      >
        {t("roles.create")}
      </Button>
    ));

  // Who uses a role, as `role.in_use` counts them: its members and its pending invitations, an expired one included.
  const usersOf = (
    role: RoleDetailResponse,
    both: "roles.usageBoth" | "roles.inUseBoth",
  ) => {
    const people =
      role.memberCount > 0 &&
      t("roles.usageMembers", { count: role.memberCount });
    const invitations =
      role.pendingInvitationCount > 0 &&
      t("roles.usageInvitations", { count: role.pendingInvitationCount });
    if (people && invitations) return t(both, { members: people, invitations });
    return people || invitations || null;
  };

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
      key: "use",
      header: t("roles.usedBy"),
      secondary: true,
      card: "hide",
      render: (role) => usersOf(role, "roles.usageBoth") ?? t("roles.notInUse"),
    },
    {
      key: "type",
      header: t("roles.type"),
      card: "end",
      render: (role) => role.builtIn && <Tag>{t("roles.builtIn")}</Tag>,
    },
  ];

  // The members of a role in the members list filtered by it (F9), or its invitations when only invitations use it.
  const usedBy = (role: RoleDetailResponse): RowMenuItem[] => {
    if (role.memberCount > 0)
      return can(Permission.TenantMembersRead)
        ? [
            {
              label: t("roles.seeMembers"),
              onSelect: () => router.push(`${members}?roleId=${role.id}`),
            },
          ]
        : [];
    if (
      role.pendingInvitationCount > 0 &&
      can(Permission.TenantInvitationsRead)
    )
      return [
        {
          label: t("roles.seeInvitations"),
          onSelect: () =>
            router.push(`${members}?tab=invitations&roleId=${role.id}`),
        },
      ];
    return [];
  };

  const itemsFor = (role: RoleDetailResponse): RowMenuItem[] => {
    const view = {
      label: t("roles.viewPermissions"),
      onSelect: () => open(role),
    };
    if (role.builtIn) return [view, ...usedBy(role)];
    const outOfReach = holdsAll(detail.permissions, role)
      ? undefined
      : t("roles.outOfReach");
    const closed = outOfReach ?? featureOff;
    const edit = {
      label: t("roles.edit"),
      onSelect: () => open(role),
      disabled: closed !== undefined,
      reason: closed,
    };
    const items: RowMenuItem[] = [
      canUpdate && !closed ? edit : view,
      ...usedBy(role),
    ];
    if (canUpdate && closed) items.push(edit);
    if (canDelete) {
      // The escalation reason wins: the viewer could not delete the role even when nobody used it (F51).
      const users = usersOf(role, "roles.inUseBoth");
      const reason =
        outOfReach ?? (users ? t("roles.inUseReason", { users }) : undefined);
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
    }
    return items;
  };

  const body = (() => {
    if (roles.isPending)
      return <Skeleton rows={4} label={t("roles.loading")} />;
    if (roles.isError)
      return (
        <ListError
          title={t("roles.loadError")}
          problem={problemOf(roles.error)}
          leave={leaveOf(roles.error)}
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
        {canCreate && (
          <div className="flex flex-col items-end gap-1">
            {createButton}
            {roles.data && counter && (
              <span
                id={counterId}
                className="max-w-90 text-end text-small text-muted-foreground"
              >
                {counter}
              </span>
            )}
          </div>
        )}
      </div>
      {canCreate && over && roleLimit.limit !== null && (
        <PlanBanner
          id={bannerId}
          title={t("plans.rolesOver", {
            count: roleLimit.count,
            limit: roleLimit.limit,
          })}
          text={t("plans.rolesOverText")}
        />
      )}
      <p className="max-w-prose text-muted-foreground">{t("roles.lead")}</p>
      {body}
      {deleting && (
        <DeleteRoleDialog role={deleting} onClose={() => setDeleting(null)} />
      )}
    </RolesFrame>
  );
}
