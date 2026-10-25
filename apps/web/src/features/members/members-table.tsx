"use client";

import {
  ListMembersSort,
  type ListPageOfMemberResponse,
  type MemberResponse,
  Permission,
} from "@repo/api-client";
import { Avatar } from "@repo/ui/components/avatar";
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
    asc: ListMembersSort.LastName,
    desc: ListMembersSort.LastNameDescending,
  },
  role: { asc: ListMembersSort.Role, desc: ListMembersSort.RoleDescending },
};

/**
 * The Members tab's table (the MembersOwner, MembersAdmin and MembersMember boards): Name with the email, Role, Joined. The row
 * menu follows F5 and F51; nobody removes themselves. A viewer who may neither change nor remove sees no menu. A disabled item
 * says why.
 */
export function MembersTable({
  page,
  sort,
  meId,
  timeZone,
  onSortChange,
  onPageChange,
}: {
  page: ListPageOfMemberResponse;
  sort: string | null;
  meId: string | undefined;
  timeZone: string | null | undefined;
  onSortChange: (sort: string | null) => void;
  onPageChange: (page: number) => void;
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
      header: t("members.name"),
      sortable: true,
      render: (member) => (
        <span className="flex min-w-0 items-center gap-3">
          <Avatar person={member} />
          <span className="flex min-w-0 flex-col">
            {name(member)}
            {member.email && (
              <bdi
                dir="ltr"
                className="truncate text-small text-muted-foreground"
              >
                {member.email}
              </bdi>
            )}
          </span>
        </span>
      ),
      cardRender: name,
    },
    {
      key: "role",
      header: t("members.role"),
      sortable: true,
      card: "end",
      render: roleText,
    },
    {
      key: "joined",
      header: t("members.joined"),
      secondary: true,
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
        rows={page.items}
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
        cardLead={(member) => <Avatar person={member} />}
        rowCount={page.total}
        page={page.page}
        pageSize={page.pageSize}
        onPageChange={onPageChange}
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
