"use client";

import {
  ListMembersSort,
  type MemberResponse,
  Permission,
} from "@repo/api-client";
import { Avatar } from "@repo/ui/components/avatar";
import {
  CalendarIcon,
  Icon,
  type LucideIcon,
  MailIcon,
  ShieldIcon,
  UserIcon,
} from "@repo/ui/components/icon";
import { Label } from "@repo/ui/components/label";
import { RowMenu, type RowMenuItem } from "@repo/ui/components/row-menu";
import {
  sortFrom,
  sortOf,
  Table,
  type TableColumn,
} from "@repo/ui/components/table";
import { Tag } from "@repo/ui/components/tag";
import { Time } from "@repo/ui/components/time";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useCan } from "@/shared/tenant-context";
import { useRoleName } from "@/shared/use-role-name";
import { ChangeRoleDialog } from "./change-role-dialog";
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
const header = (icon: LucideIcon, label: string) => (
  <span className="inline-flex items-center gap-1.5">
    <Icon icon={icon} size={13} />
    {label}
  </span>
);

/**
 * The Members tab's table (8.01): Name with the avatar, Email, Role as a Label, Joined. The rows are every page loaded so far
 * (F9, F10); the header still sorts by Name and Role. The row menu follows F5 and F51; nobody removes themselves. A viewer who
 * may neither change nor remove sees no menu. A disabled item says why.
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
  const canChange = can(Permission.TenantMembersUpdate);
  const canRemove = can(Permission.TenantMembersDelete);
  const nameText = (member: MemberResponse) =>
    nameOf(member) ?? t("members.deletedUser");
  const roleText = (member: MemberResponse) => roleName(member.role);

  const name = (member: MemberResponse) => (
    <span className="flex items-center gap-2">
      <bdi className="truncate font-medium">{nameText(member)}</bdi>
      {member.userId === meId && <Tag variant="text">{t("members.you")}</Tag>}
    </span>
  );

  const columns: TableColumn<MemberResponse>[] = [
    {
      key: "name",
      header: header(UserIcon, t("members.name")),
      sortable: true,
      render: (member) => (
        <span className="flex min-w-0 items-center gap-2">
          <Avatar person={member} className="size-5.5" />
          {name(member)}
        </span>
      ),
      cardRender: name,
    },
    {
      key: "email",
      header: header(MailIcon, t("members.email")),
      secondary: true,
      card: "meta",
      // The email reads left to right in every language; a long one ends with "…".
      render: (member) =>
        member.email && (
          <bdi dir="ltr" className="block truncate text-small">
            {member.email}
          </bdi>
        ),
    },
    {
      key: "role",
      header: header(ShieldIcon, t("members.role")),
      sortable: true,
      card: "end",
      render: (member) => <Label>{roleText(member)}</Label>,
    },
    {
      key: "joined",
      header: header(CalendarIcon, t("members.joined")),
      secondary: true,
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
    const self = member.userId === meId;
    const changeReason = reach(member.role.id, t("members.changeOwner"));
    const removeReason = self
      ? t("members.removeSelf")
      : reach(member.role.id, t("members.removeOwner"));
    const items: RowMenuItem[] = [];
    if (canChange)
      items.push({
        label: t("members.changeRole"),
        onSelect: () => setChanging(member),
        disabled: changeReason !== null,
        reason: changeReason ?? undefined,
      });
    if (canChange && canRemove) items.push({ separator: true });
    if (canRemove)
      items.push({
        label: t("members.remove"),
        tone: "destructive",
        onSelect: () => setRemoving(member),
        disabled: removeReason !== null,
        reason: removeReason ?? undefined,
      });
    return items;
  };

  return (
    <>
      <Table
        columns={columns}
        rows={rows}
        rowKey="userId"
        label={t("members.title")}
        actions={
          canChange || canRemove
            ? (member) => (
                <RowMenu
                  label={t("members.actionsFor", { name: nameText(member) })}
                  items={itemsFor(member)}
                />
              )
            : undefined
        }
        actionsLabel={t("members.actions")}
        cardLead={(member) => <Avatar person={member} className="size-9" />}
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
    </>
  );
}
