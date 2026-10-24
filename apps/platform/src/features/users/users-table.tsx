"use client";

import {
  type ListPageOfUserResponse,
  ListUsersSort,
  type UserResponse,
} from "@repo/api-client";
import { Avatar } from "@repo/ui/components/avatar";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Status } from "@repo/ui/components/status";
import {
  sortFrom,
  sortOf,
  Table,
  type TableColumn,
} from "@repo/ui/components/table";
import { Tag } from "@repo/ui/components/tag";
import { Time } from "@repo/ui/components/time";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { settingsPages } from "@/shared/settings-nav";
import type { useUserActions } from "./use-user-actions";
import { actionsOf, displayNameOf, statuses } from "./user";

/** The column a person can sort by, and the API's sort field (K5). The email's sort works from the URL. */
const sorts = {
  user: { asc: ListUsersSort.LastName, desc: ListUsersSort.LastNameDescending },
};

/**
 * The System admin tag, and Root beside it. In the list they are information only (the Users board); on a user's page each one
 * links to System admins (the UserDetail board).
 */
export function AdminTags({
  user,
  linked = false,
}: {
  user: Pick<UserResponse, "isSystemAdmin" | "isRootSystemAdmin">;
  linked?: boolean;
}) {
  const t = useTranslations("users");
  const tag = (label: string) =>
    linked ? (
      <Link
        href={settingsPages.systemAdmins}
        className="rounded-md hover:underline"
      >
        <Tag>{label}</Tag>
      </Link>
    ) : (
      <Tag>{label}</Tag>
    );
  return (
    <>
      {user.isSystemAdmin && tag(t("systemAdmin"))}
      {user.isRootSystemAdmin && tag(t("root"))}
    </>
  );
}

/**
 * The list of users across every organisation (the Users board): the person with their email and tags, Organisations
 * (`membershipCount`), Status and Created. A deleted user has no name and no email, and no menu.
 */
export function UsersTable({
  page,
  sort,
  timeZone,
  actions,
  onSortChange,
  onPageChange,
}: {
  page: ListPageOfUserResponse;
  sort: string | null;
  timeZone: string | null | undefined;
  actions: ReturnType<typeof useUserActions>;
  onSortChange: (sort: string | null) => void;
  onPageChange: (page: number) => void;
}) {
  const t = useTranslations();
  const locale = t("locale.format");
  const nameText = (user: UserResponse) =>
    displayNameOf(user, t("users.deletedUser"));

  const name = (user: UserResponse) => (
    <Link
      href={`/users/${user.id}`}
      className="truncate font-medium hover:underline"
    >
      <bdi>{nameText(user)}</bdi>
    </Link>
  );

  const columns: TableColumn<UserResponse>[] = [
    {
      key: "user",
      header: t("users.user"),
      sortable: true,
      render: (user) => (
        <span className="flex min-w-0 items-center gap-3">
          <Avatar
            person={{ firstName: user.firstName, lastName: user.lastName }}
          />
          <span className="flex min-w-0 flex-col">
            <span className="flex min-w-0 flex-wrap items-center gap-2">
              {name(user)}
              <AdminTags user={user} />
            </span>
            {user.email && (
              <bdi
                dir="ltr"
                className="truncate text-small text-muted-foreground"
              >
                {user.email}
              </bdi>
            )}
          </span>
        </span>
      ),
      cardRender: name,
    },
    {
      key: "organisations",
      header: t("users.organisations"),
      align: "end",
      card: "hide",
      render: (user) => user.membershipCount,
    },
    {
      key: "status",
      header: t("users.status"),
      card: "end",
      render: (user) => (
        <Status tone={statuses[user.status].tone}>
          {t(statuses[user.status].label)}
        </Status>
      ),
    },
    {
      key: "created",
      header: t("users.created"),
      secondary: true,
      card: "hide",
      render: (user) => (
        <Time
          at={user.createdAt}
          show="date"
          locale={locale}
          timeZone={timeZone}
        />
      ),
    },
  ];

  return (
    <Table
      columns={columns}
      rows={page.items}
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
      cardLead={(user) => (
        <Avatar
          person={{ firstName: user.firstName, lastName: user.lastName }}
        />
      )}
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
  );
}
