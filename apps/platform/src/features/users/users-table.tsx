"use client";

import {
  ListUsersSort,
  type UserResponse,
  UserResponseStatus,
} from "@repo/api-client";
import { Label } from "@repo/ui/components/label";
import { Pill } from "@repo/ui/components/pill";
import { RowMenu } from "@repo/ui/components/row-menu";
import {
  sortFrom,
  sortOf,
  Table,
  type TableColumn,
} from "@repo/ui/components/table";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { userNameOf } from "@/shared/name-of";
import type { useUserActions } from "./use-user-actions";
import { actionsOf, statuses } from "./user";

/** The column a person can sort by, and the API's sort field (K5). The email's sort works from the URL. */
const sorts = {
  name: { asc: ListUsersSort.Name, desc: ListUsersSort.NameDescending },
};

/** A user's status as its Pill (5.01). */
export function UserStatus({ status }: { status: UserResponseStatus }) {
  const t = useTranslations();
  return <Pill tone={statuses[status].tone}>{t(statuses[status].label)}</Pill>;
}

/**
 * The list of users across every organisation (5.01, 5.16): Name with the System admin label, Status, Email and Organisations
 * (`membershipCount`), with no avatar. A phone card shows the email and the count, and at its end a status that is not active, or
 * the System admin label. A deleted user has no name and no email, and no menu.
 */
export function UsersTable({
  rows,
  sort,
  actions,
  onSortChange,
}: {
  rows: UserResponse[];
  sort: string | null;
  actions: ReturnType<typeof useUserActions>;
  onSortChange: (sort: string | null) => void;
}) {
  const t = useTranslations();
  const nameText = (user: UserResponse) =>
    userNameOf(user, t("users.deletedUser"));
  const name = (user: UserResponse) => (
    <Link href={`/users/${user.id}`} className="truncate hover:underline">
      <bdi>{nameText(user)}</bdi>
    </Link>
  );
  const systemAdmin = (user: UserResponse) =>
    user.isSystemAdmin && <Label>{t("users.systemAdmin")}</Label>;
  const email = (user: UserResponse) =>
    user.email && <bdi dir="ltr">{user.email}</bdi>;

  const columns: TableColumn<UserResponse>[] = [
    {
      key: "name",
      header: t("users.name"),
      sortable: true,
      render: (user) => (
        <span className="flex min-w-0 items-center gap-2">
          {name(user)}
          {systemAdmin(user)}
        </span>
      ),
      cardRender: name,
    },
    {
      key: "status",
      header: t("users.status"),
      width: "w-38.5",
      card: "end",
      render: (user) => <UserStatus status={user.status} />,
      cardRender: (user) =>
        user.status === UserResponseStatus.Active ? (
          systemAdmin(user)
        ) : (
          <UserStatus status={user.status} />
        ),
    },
    {
      key: "email",
      header: t("users.email"),
      width: "w-53.5",
      render: (user) => <span className="block truncate">{email(user)}</span>,
      // The card's one line: the email, then the count when there is one (5.16).
      cardRender: (user) => (
        <>
          {email(user)}
          {user.email && user.membershipCount > 0 && " · "}
          {user.membershipCount > 0 &&
            t("users.organisationCount", { count: user.membershipCount })}
        </>
      ),
    },
    {
      key: "organisations",
      header: t("users.organisations"),
      width: "w-43.5",
      card: "hide",
      render: (user) => user.membershipCount,
    },
  ];

  return (
    <Table
      columns={columns}
      rows={rows}
      rowKey="id"
      label={t("users.title")}
      actions={(user) =>
        actionsOf(user).length > 0 && (
          <RowMenu
            label={t("users.actionsFor", { name: nameText(user) })}
            items={actions.itemsFor(user)}
          />
        )
      }
      actionsLabel={t("users.actions")}
      rowCount={rows.length}
      page={1}
      pageSize={Math.max(rows.length, 1)}
      sort={sortOf(sorts, sort)}
      onSortChange={(next) => onSortChange(sortFrom(sorts, next))}
      pager={{
        previous: t("common.previous"),
        next: t("common.next"),
        page: (current, count) => t("common.page", { page: current, count }),
      }}
    />
  );
}
