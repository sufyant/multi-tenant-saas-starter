"use client";

import {
  getListMembersQueryOptions,
  ListMembersSort,
  type MemberResponse,
  Permission,
} from "@repo/api-client";
import { Avatar } from "@repo/ui/components/avatar";
import { LogOutIcon, ShieldIcon, TrashIcon } from "@repo/ui/components/icon";
import { Label } from "@repo/ui/components/label";
import { RowMenu, type RowMenuItem } from "@repo/ui/components/row-menu";
import {
  sortFrom,
  sortOf,
  Table,
  type TableColumn,
} from "@repo/ui/components/table";
import { Time } from "@repo/ui/components/time";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useCan, useTenant } from "@/shared/tenant-context";
import { useRoleName } from "@/shared/use-role-name";
import { ChangeRoleDialog } from "./change-role-dialog";
import { LeaveDialog } from "./leave-dialog";
import { nameOf } from "./person";
import { RemoveMemberDialog } from "./remove-member-dialog";
import { useRoleReach } from "./use-role-reach";

/** The columns a person can sort by, and the API's sort field of each (K5). */
const sorts = {
  name: {
    asc: ListMembersSort.Name,
    desc: ListMembersSort.NameDescending,
  },
  role: { asc: ListMembersSort.Role, desc: ListMembersSort.RoleDescending },
};

/** A column's header: its 13 px icon before the label (8.01). */
/**
 * The Members tab's table (8.01): Name with the avatar, Email, Role as a Label, Joined. The rows are every page loaded so far
 * (F9, F10); the header still sorts by Name and Role. The row menu follows F5 and F51; nobody removes themselves: the person's own
 * row offers Leave in its place, to every role (8.18). Another row without an action the viewer may take has no menu. A disabled
 * item says why: the last Owner cannot leave, which ListMembers filtered by the Owner role tells before the click (8.46).
 */
export function MembersTable({
  rows,
  sort,
  meId,
  timeZone,
  onSortChange,
}: {
  rows: MemberResponse[];
  sort: string | null;
  meId: string | undefined;
  timeZone: string | null | undefined;
  onSortChange: (sort: string | null) => void;
}) {
  const t = useTranslations();
  const roleName = useRoleName();
  const can = useCan();
  const reach = useRoleReach();
  const [changing, setChanging] = useState<MemberResponse | null>(null);
  const [removing, setRemoving] = useState<MemberResponse | null>(null);
  const [leaving, setLeaving] = useState(false);
  const canChange = can(Permission.TenantMembersUpdate);
  const canRemove = can(Permission.TenantMembersDelete);
  const { tenant, detail } = useTenant();
  // Only the Owner role manages Owners (F51), so the viewer is an Owner, and their role is the Owner role. The Owners' total is one
  // item of the list filtered by that role, read only then.
  const selfOwner = can(Permission.TenantOwnersManage);
  const owners = useQuery(
    getListMembersQueryOptions(
      tenant.id,
      { roleId: detail.role.id, pageSize: 1 },
      { query: { enabled: selfOwner } },
    ),
  );
  const lastOwner = selfOwner && owners.data?.total === 1;
  const nameText = (member: MemberResponse) =>
    nameOf(member) ?? t("members.deletedUser");
  const roleText = (member: MemberResponse) => roleName(member.role);

  const name = (member: MemberResponse) => (
    <span className="flex min-w-0 items-center gap-2">
      <bdi className="truncate font-medium">{nameText(member)}</bdi>
      {member.userId === meId && (
        <span className="shrink-0 text-status text-muted-foreground">
          {t("members.you")}
        </span>
      )}
    </span>
  );

  const columns: TableColumn<MemberResponse>[] = [
    {
      key: "name",
      header: t("members.name"),
      sortable: true,
      // Name fills the table's free width and a long one ends with "…": its cell takes the full width with no width of its own
      // (`max-w-0`), so the table never grows wider than the page (8.05).
      render: (member) => (
        <span className="flex min-w-0 items-center gap-2 [td:has(>&)]:w-full [td:has(>&)]:max-w-0">
          <Avatar person={{ ...member, id: member.userId }} />
          {name(member)}
        </span>
      ),
      cardRender: name,
    },
    {
      key: "email",
      header: t("members.email"),
      secondary: true,
      width: "w-66",
      card: "meta",
      // The email reads left to right in every language, inside a block of the page's direction, so in Arabic it aligns with its
      // header (8.36). In the table a long one ends with "…" at 250 px, the frame's width (8.05).
      render: (member) =>
        member.email && (
          <span className="block truncate text-small @min-[640px]:max-w-62.5">
            <bdi dir="ltr">{member.email}</bdi>
          </span>
        ),
    },
    {
      key: "role",
      header: t("members.role"),
      sortable: true,
      width: "w-41",
      card: "end",
      render: (member) => <Label>{roleText(member)}</Label>,
    },
    {
      key: "joined",
      header: t("members.joined"),
      secondary: true,
      width: "w-31",
      // Joined hides in a table narrower than the desktop's (8.33).
      show: "wide",
      card: "hide",
      render: (member) => (
        <Time
          at={member.joinedAt}
          show="date"
          locale={t("locale.format")}
          timeZone={timeZone}
        />
      ),
    },
  ];

  const itemsFor = (member: MemberResponse): RowMenuItem[] => {
    const own = member.userId === meId;
    const changeReason = reach(member.role.id, t("members.changeOwner"));
    const removeReason = reach(member.role.id, t("members.removeOwner"));
    const items: RowMenuItem[] = [];
    if (canChange)
      items.push({
        label: t("members.changeRole"),
        icon: ShieldIcon,
        onSelect: () => setChanging(member),
        disabled: changeReason !== null,
        reason: changeReason ?? undefined,
      });
    const last: RowMenuItem | null = own
      ? {
          label: t("members.leave"),
          icon: LogOutIcon,
          tone: "destructive",
          onSelect: () => setLeaving(true),
          // The Owners' count still loads: the item waits, disabled. When the count fails, the API decides (8.25).
          disabled: lastOwner || (selfOwner && owners.isPending),
          reason: lastOwner ? t("members.leaveLastOwner") : undefined,
        }
      : canRemove
        ? {
            label: t("members.remove"),
            icon: TrashIcon,
            tone: "destructive",
            onSelect: () => setRemoving(member),
            disabled: removeReason !== null,
            reason: removeReason ?? undefined,
          }
        : null;
    if (last) items.push(last);
    return items;
  };

  return (
    <>
      <Table
        columns={columns}
        rows={rows}
        rowKey="userId"
        label={t("members.title")}
        actions={(member) => {
          const items = itemsFor(member);
          return (
            items.length > 0 && (
              <RowMenu
                label={t("members.actionsFor", { name: nameText(member) })}
                items={items}
              />
            )
          );
        }}
        actionsLabel={t("members.actions")}
        cardLead={(member) => (
          <Avatar person={{ ...member, id: member.userId }} size="medium" />
        )}
        // Every loaded row is on the one page: the list grows with Load more, so the pager never shows (F9, F10).
        rowCount={rows.length}
        page={1}
        pageSize={Math.max(1, rows.length)}
        sort={sortOf(sorts, sort)}
        onSortChange={(next) => onSortChange(sortFrom(sorts, next))}
        pager={{
          previous: t("common.previous"),
          next: t("common.next"),
          page: (current, count) => t("common.page", { page: current, count }),
        }}
      />
      {changing && (
        <ChangeRoleDialog
          member={changing}
          self={changing.userId === meId}
          onClose={() => setChanging(null)}
        />
      )}
      {removing && (
        <RemoveMemberDialog
          member={removing}
          onClose={() => setRemoving(null)}
        />
      )}
      {leaving && <LeaveDialog onClose={() => setLeaving(false)} />}
    </>
  );
}
