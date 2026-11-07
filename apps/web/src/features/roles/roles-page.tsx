"use client";

import {
  actionFor,
  FeatureKey,
  getListRolesInfiniteQueryOptions,
  LimitKey,
  Permission,
  ProblemDetailsError,
  problemOf,
  type RoleDetailResponse,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Dialog } from "@repo/ui/components/dialog";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Icon, PlusIcon } from "@repo/ui/components/icon";
import { Label } from "@repo/ui/components/label";
import { ListError } from "@repo/ui/components/list-error";
import { RowMenu, type RowMenuItem } from "@repo/ui/components/row-menu";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useInfiniteQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useId, useState } from "react";
import { LoadMore } from "@/shared/load-more";
import { nextPage, pageSize, rowsIn } from "@/shared/pages";
import { PermissionDenied } from "@/shared/permission-denied";
import { usePlan } from "@/shared/plan";
import { PlanBanner, reasonIdsOf } from "@/shared/plan-banner";
import { leaveOf } from "@/shared/problem-screen";
import { inListOrder } from "@/shared/roles";
import { TableSkeleton } from "@/shared/table-skeleton";
import { useCan, useTenant } from "@/shared/tenant-context";
import { holdsAll } from "@/shared/use-can-act-on";
import { sentenceOf, usePlanReason } from "@/shared/use-plan-reason";
import { useRoleName } from "@/shared/use-role-name";
import { DeleteRoleDialog } from "./delete-role-dialog";
import { useRoleUsers } from "./role-users";
import { RolesFrame } from "./roles-frame";

/** The bars of the first load, by column: Name, Type, Used by, Permissions (9.02). */
const skeletonBars = ["w-30", "w-30", "w-30", "w-30"];

/** The list of a role's members or invitations, as a menu item and as the in-use dialog's action. */
type UsersLink = { label: string; onSelect: () => void };

/** A role in use, whose "Delete role" says who uses it and opens their list (9.13). */
type InUse = { role: RoleDetailResponse; link: UsersLink | null };

/**
 * Settings > Roles (9.01 to 9.07, 9.12, 9.13, 9.17): Name, Type, Used by and Permissions, the built-in roles first (F51), under the
 * frame's description and no Freshness (F59). The list grows with Load more (F9). A built-in role offers "View permissions". A
 * custom role offers "Edit role" and "Delete role" by their permissions (F5), both disabled with the reason when the viewer's role
 * does not hold every permission of it (F51); without the update permission it opens read-only. A role in use offers its members,
 * or its invitations when only invitations use it; its "Delete role" opens a dialog that says who uses it and offers the same list
 * (9.13), and calls no delete. The plan (through `usePlan`, F56 until 40a): a viewer who may create sees the count of
 * `roles.custom.max` (none when unlimited); at the limit "New role" is disabled with the reason, over it with the banner. With
 * `roles.custom` off the custom roles stay listed, "New role" and "Edit role" are disabled with the feature's reason in place of
 * the count, and "Delete role" keeps its own rules.
 */
export function RolesPage() {
  const t = useTranslations();
  const roleName = useRoleName();
  const usersOf = useRoleUsers();
  const can = useCan();
  const router = useRouter();
  const { tenant, detail } = useTenant();
  const failureText = useFailureText();
  const readsRoles = can(Permission.TenantRolesRead);
  const roles = useInfiniteQuery(
    getListRolesInfiniteQueryOptions(
      tenant.id,
      { pageSize },
      {
        query: {
          initialPageParam: 1,
          getNextPageParam: nextPage,
          enabled: readsRoles,
        },
      },
    ),
  );
  const plan = usePlan();
  const planReason = usePlanReason();
  const [deleting, setDeleting] = useState<RoleDetailResponse | null>(null);
  const [inUse, setInUse] = useState<InUse | null>(null);
  const [announcement, setAnnouncement] = useState<string | null>(null);
  const counterId = useId();
  const bannerId = useId();

  if (!readsRoles)
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
  const plus = <Icon icon={PlusIcon} size={16} />;
  // The button waits for the roles, so it shows with the list it acts on.
  const createButton =
    canCreate &&
    roles.data !== undefined &&
    (featureOff || roleLimit.full ? (
      <Button
        variant="primary"
        icon={plus}
        disabled
        aria-describedby={over ? reasonIdsOf(bannerId) : counterId}
      >
        {t("roles.newRole")}
      </Button>
    ) : (
      <Button
        variant="primary"
        icon={plus}
        render={<Link href={`${base}/new`} />}
        nativeButton={false}
      >
        {t("roles.newRole")}
      </Button>
    ));

  const name = (role: RoleDetailResponse) => (
    <bdi className="block truncate font-medium">{roleName(role)}</bdi>
  );
  const columns: TableColumn<RoleDetailResponse>[] = [
    { key: "name", header: t("roles.name"), render: name },
    {
      key: "type",
      header: t("roles.type"),
      card: "end",
      render: (role) => (
        <Label>{role.builtIn ? t("roles.builtIn") : t("roles.custom")}</Label>
      ),
    },
    {
      key: "use",
      header: t("roles.usedBy"),
      secondary: true,
      card: "hide",
      render: (role) => usersOf(role, "usageBoth") ?? t("roles.notInUse"),
    },
    {
      key: "permissions",
      header: t("roles.permissions"),
      secondary: true,
      render: (role) =>
        t("members.permissionCount", { count: role.permissions.length }),
    },
  ];

  // The members of a role in the members list filtered by it (F9), or its invitations when only invitations use it.
  const usedByLink = (role: RoleDetailResponse): UsersLink | null => {
    if (role.memberCount > 0)
      return can(Permission.TenantMembersRead)
        ? {
            label: t("roles.seeMembers"),
            onSelect: () => router.push(`${members}?roleId=${role.id}`),
          }
        : null;
    if (
      role.pendingInvitationCount > 0 &&
      can(Permission.TenantInvitationsRead)
    )
      return {
        label: t("roles.seeInvitations"),
        onSelect: () =>
          router.push(`${members}?tab=invitations&roleId=${role.id}`),
      };
    return null;
  };
  const usedBy = (role: RoleDetailResponse): RowMenuItem[] => {
    const link = usedByLink(role);
    return link ? [link] : [];
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
      // The escalation reason wins: the viewer could not delete the role even when nobody used it (F51). A role in use opens
      // the dialog of 9.13, which says who uses it (the rule of D4: the action stays and says why).
      const used = role.memberCount > 0 || role.pendingInvitationCount > 0;
      items.push(
        { separator: true },
        {
          label: t("roles.delete"),
          tone: "destructive",
          onSelect: () =>
            used
              ? setInUse({ role, link: usedByLink(role) })
              : setDeleting(role),
          disabled: outOfReach !== undefined,
          reason: outOfReach,
        },
      );
    }
    return items;
  };

  const rowCount = rowsIn(roles.data);
  // The next page: a 401 goes to sign-in (F4), a 403 of a stopped organisation is the tenant layout's (F47), and any other
  // failure says so and keeps the rows that came.
  const loadMore = async () => {
    const before = rowCount ?? 0;
    const result = await roles.fetchNextPage();
    if (!result.isFetchNextPageError) {
      setAnnouncement(
        t("roles.loadedRoles", { count: (rowsIn(result.data) ?? 0) - before }),
      );
      return;
    }
    const action =
      result.error instanceof ProblemDetailsError
        ? actionFor(result.error.status, result.error.problem).kind
        : null;
    if (action === "sign-in")
      return window.location.assign(signInAgainUrl(window.location));
    if (action === "tenant-blocked") return;
    showToast({
      title: t("roles.moreError"),
      description: failureText(problemOf(result.error)),
      tone: "destructive",
    });
  };

  const body = (() => {
    if (roles.isPending)
      return (
        <TableSkeleton
          columns={columns.map((column) => column.header)}
          bars={skeletonBars}
          rows={7}
          label={t("roles.loading")}
        />
      );
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
    const rows = inListOrder(roles.data.pages.flatMap((page) => page.items));
    return (
      <div className="flex flex-col gap-2">
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
          // Every loaded row is on the one page: the list grows with Load more, so the pager never shows (F9, F10).
          rowCount={rows.length}
          page={1}
          pageSize={Math.max(1, rows.length)}
          pager={{
            previous: t("common.previous"),
            next: t("common.next"),
            page: (current, count) =>
              t("common.page", { page: current, count }),
          }}
        />
        <LoadMore
          hasMore={roles.hasNextPage}
          loading={roles.isFetchingNextPage}
          onMore={() => void loadMore()}
          label={t("roles.loadMore")}
          loadingLabel={t("roles.loadingMore")}
          announcement={announcement}
        />
        {!roles.hasNextPage && rows.every((role) => role.builtIn) && (
          <EmptyState
            title={t("roles.noCustom")}
            description={t("roles.noCustomText")}
          />
        )}
      </div>
    );
  })();

  return (
    <RolesFrame>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex min-w-0 flex-col gap-0.5">
          <h1 className="text-page-title">{t("roles.title")}</h1>
          <p className="text-body text-muted-foreground">{t("roles.lead")}</p>
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
      {body}
      {deleting && (
        <DeleteRoleDialog role={deleting} onClose={() => setDeleting(null)} />
      )}
      {inUse && (
        <RoleInUseDialog
          role={inUse.role}
          users={usersOf(inUse.role, "inUseBoth") ?? ""}
          link={inUse.link}
          onClose={() => setInUse(null)}
        />
      )}
    </RolesFrame>
  );
}

/** "{name} is in use" (9.13): who uses the role, and their list when the viewer may read it; without it the dialog only informs. */
function RoleInUseDialog({
  role,
  users,
  link,
  onClose,
}: {
  role: RoleDetailResponse;
  users: string;
  link: UsersLink | null;
  onClose: () => void;
}) {
  const t = useTranslations();
  const roleName = useRoleName();
  const kind = (() => {
    if (role.pendingInvitationCount === 0) return "members";
    return role.memberCount === 0 ? "invitations" : "both";
  })();
  const close = (open: boolean) => !open && onClose();
  const text = t("roles.inUseDialog", {
    users,
    total: role.memberCount + role.pendingInvitationCount,
    kind,
  });
  if (!link)
    return (
      <Dialog
        open
        onOpenChange={close}
        title={t("roles.inUseTitle", { name: roleName(role) })}
        description={text}
        actionLabel={t("common.close")}
        onConfirm={onClose}
      />
    );
  return (
    <Dialog
      open
      onOpenChange={close}
      title={t("roles.inUseTitle", { name: roleName(role) })}
      description={text}
      actionLabel={link.label}
      cancelLabel={t("common.close")}
      onConfirm={() => {
        onClose();
        link.onSelect();
      }}
    />
  );
}
