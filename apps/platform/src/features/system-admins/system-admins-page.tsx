"use client";

import {
  getGetMeQueryOptions,
  getListUsersQueryOptions,
  problemOf,
  type UserResponse,
} from "@repo/api-client";
import { Avatar } from "@repo/ui/components/avatar";
import { Button } from "@repo/ui/components/button";
import { Freshness } from "@repo/ui/components/freshness";
import { ListError } from "@repo/ui/components/list-error";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { Tag } from "@repo/ui/components/tag";
import { Time } from "@repo/ui/components/time";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { parseAsInteger, useQueryState } from "nuqs";
import { useState } from "react";
import { leaveOf } from "@/shared/problem-screen";
import { SettingsNav } from "@/shared/settings-nav";
import { useDates } from "@/shared/use-dates";
import { AddDialog } from "./add-dialog";
import { RemoveDialog } from "./remove-dialog";
import { adminNameOf } from "./system-admin";

/** The page of the list lives in the URL, and each change pushes a history entry (F9). */
const pageParser = parseAsInteger.withDefault(1).withOptions({
  history: "push",
});

/**
 * Settings › System admins (the SystemAdmins board): ListUsers with `isSystemAdmin=true`, each admin with their email, the Root tag
 * and the date added (`systemAdminGrantedAt`). A root admin has no action: the configuration names them. The viewer's own row
 * has Remove disabled. There is no empty state: there is always a root admin, although one who never signed in is not listed.
 * Every system admin holds every platform permission (the API's A4), so nothing checks one; the API still decides.
 */
export function SystemAdminsPage() {
  const t = useTranslations();
  const { timeZone } = useDates();
  const [page, setPage] = useQueryState("page", pageParser);
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState<UserResponse | null>(null);
  const me = useQuery(getGetMeQueryOptions());
  const list = useQuery(
    getListUsersQueryOptions(
      { isSystemAdmin: true, page },
      { query: { placeholderData: keepPreviousData } },
    ),
  );
  const locale = t("locale.format");

  const person = (user: UserResponse) => (
    <span className="flex min-w-0 items-center gap-3">
      <Avatar person={{ firstName: user.firstName, lastName: user.lastName }} />
      <span className="flex min-w-0 flex-col">
        <span className="flex min-w-0 flex-wrap items-center gap-2">
          <bdi className="truncate font-medium">{adminNameOf(user)}</bdi>
          {user.isRootSystemAdmin && <Tag>{t("systemAdmins.root")}</Tag>}
          {user.id === me.data?.id && <Tag>{t("systemAdmins.you")}</Tag>}
        </span>
        {user.email && (
          <bdi dir="ltr" className="truncate text-small text-muted-foreground">
            {user.email}
          </bdi>
        )}
      </span>
    </span>
  );

  const columns: TableColumn<UserResponse>[] = [
    {
      key: "name",
      header: t("systemAdmins.name"),
      render: person,
      cardRender: (user) => <bdi>{adminNameOf(user)}</bdi>,
    },
    {
      key: "added",
      header: t("systemAdmins.added"),
      secondary: true,
      card: "end",
      render: (user) =>
        user.systemAdminGrantedAt && (
          <Time
            at={user.systemAdminGrantedAt}
            show="date"
            locale={locale}
            timeZone={timeZone}
          />
        ),
    },
  ];

  const actions = (user: UserResponse) => {
    if (user.isRootSystemAdmin)
      return (
        <span className="block max-w-56 text-end text-small text-muted-foreground">
          {t("systemAdmins.rootHint")}
        </span>
      );
    const self = user.id === me.data?.id;
    return (
      <RowMenu
        label={t("systemAdmins.actionsFor", { name: adminNameOf(user) })}
        items={[
          {
            label: t("systemAdmins.remove"),
            tone: "destructive",
            disabled: self,
            reason: self ? t("systemAdmins.cannotRemoveSelf") : undefined,
            onSelect: () => setRemoving(user),
          },
        ]}
      />
    );
  };

  const body = (() => {
    if (list.isPending)
      return <Skeleton rows={3} label={t("systemAdmins.loading")} />;
    if (list.isError)
      return (
        <ListError
          title={t("systemAdmins.error")}
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    return (
      <Table
        columns={columns}
        rows={list.data.items}
        rowKey="id"
        label={t("systemAdmins.title")}
        actions={actions}
        actionsLabel={t("systemAdmins.actions")}
        cardLead={(user) => (
          <Avatar
            person={{ firstName: user.firstName, lastName: user.lastName }}
          />
        )}
        rowCount={list.data.total}
        page={list.data.page}
        pageSize={list.data.pageSize}
        onPageChange={(next) => void setPage(next)}
        pager={{
          previous: t("common.previous"),
          next: t("common.next"),
          page: (current, count) => t("common.page", { page: current, count }),
        }}
      />
    );
  })();

  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="system-admins" />
      <section className="flex min-w-0 max-w-[1160px] flex-1 flex-col gap-4">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="text-page-title">{t("systemAdmins.title")}</h1>
            {list.dataUpdatedAt > 0 && (
              <Freshness
                at={new Date(list.dataUpdatedAt)}
                locale={locale}
                timeZone={timeZone}
                label={t("common.updated")}
              />
            )}
          </div>
          <Button variant="primary" onClick={() => setAdding(true)}>
            {t("systemAdmins.add")}
          </Button>
        </div>
        <p className="max-w-[720px] text-body text-muted-foreground">
          {t("systemAdmins.text")}
        </p>
        {body}
      </section>
      {adding && <AddDialog onClose={() => setAdding(false)} />}
      {removing && (
        <RemoveDialog user={removing} onClose={() => setRemoving(null)} />
      )}
    </div>
  );
}
