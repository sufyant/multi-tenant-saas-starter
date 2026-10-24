"use client";

import {
  getListUsersQueryOptions,
  ListUsersSort,
  problemOf,
  schemas,
  UserResponseStatus,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Freshness } from "@repo/ui/components/freshness";
import { ListError } from "@repo/ui/components/list-error";
import { Search } from "@repo/ui/components/search";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Tabs } from "@repo/ui/components/tabs";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useCallback } from "react";
import { leaveOf } from "@/shared/problem-screen";
import { useDates } from "@/shared/use-dates";
import { type Tab, tabs, useListParams } from "./use-list-params";
import { useUserActions } from "./use-user-actions";
import { UsersTable } from "./users-table";

const all = "all";

/** The tab's label key: All, then the statuses (the Users board). */
const tabLabels = {
  [UserResponseStatus.Active]: "users.tabActive",
  [UserResponseStatus.Suspended]: "users.tabSuspended",
} as const satisfies Record<Tab, string>;

/** The title of an empty tab (the ListStates board). */
const noMatchLabels = {
  [UserResponseStatus.Active]: "users.noMatchActive",
  [UserResponseStatus.Suspended]: "users.noMatchSuspended",
} as const satisfies Record<Tab, string>;

/**
 * Users, the people of every organisation (the Users and ListStates boards): the status tabs, the search by name or email, and
 * the table. Every parameter of the list is in the URL (F9). The System admin and Root tags are information; the System admins
 * page manages them.
 */
export function UsersPage() {
  const t = useTranslations();
  const { timeZone } = useDates();
  const [params, setParams] = useListParams();
  const actions = useUserActions();
  const { status, q, page, sort } = params;
  const onSearch = useCallback(
    (next: string) => void setParams({ q: next || null, page: null }),
    [setParams],
  );

  const list = useQuery(
    getListUsersQueryOptions(
      {
        q: q || undefined,
        status: status ?? undefined,
        page,
        sort: sort ?? undefined,
      },
      { query: { placeholderData: keepPreviousData } },
    ),
  );

  const clear = () => void setParams({ q: null, status: null, page: null });

  const body = (() => {
    if (list.isPending) return <Skeleton rows={4} label={t("users.loading")} />;
    if (list.isError)
      return (
        <ListError
          title={t("users.error")}
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    // There is always a root system admin, so only a search or a tab finds nobody (the ListStates board).
    if (list.data.items.length === 0 && (q || status))
      return (
        <EmptyState
          kind="no-results"
          title={
            q
              ? t("users.noMatchSearch", { q })
              : status && t(noMatchLabels[status])
          }
          description={
            q ? t("users.noMatchSearchText") : t("users.noMatchText")
          }
          action={<Button onClick={clear}>{t("users.clearFilters")}</Button>}
        />
      );
    return (
      <UsersTable
        page={list.data}
        sort={sort}
        timeZone={timeZone}
        actions={actions}
        onSortChange={(next) =>
          void setParams({
            sort:
              Object.values(ListUsersSort).find((value) => value === next) ??
              null,
            page: null,
          })
        }
        onPageChange={(next) => void setParams({ page: next })}
      />
    );
  })();

  return (
    <section className="flex min-w-0 max-w-[1160px] flex-col gap-4">
      <div>
        <h1 className="text-page-title">{t("users.title")}</h1>
        {list.dataUpdatedAt > 0 && (
          <Freshness
            at={new Date(list.dataUpdatedAt)}
            locale={t("locale.format")}
            timeZone={timeZone}
            label={t("common.updated")}
          />
        )}
      </div>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <Tabs
          label={t("users.tabs")}
          current={status ?? all}
          items={[
            { id: all, label: t("users.tabAll") },
            ...tabs.map((tab) => ({ id: tab, label: t(tabLabels[tab]) })),
          ]}
          onValueChange={(next) =>
            void setParams({
              status: tabs.find((tab) => tab === next) ?? null,
              page: null,
            })
          }
        />
        <Search
          value={q}
          label={t("users.search")}
          maxLength={schemas.listUsersQueryQMax}
          onSearch={onSearch}
        />
      </div>
      {body}
      {actions.dialog}
    </section>
  );
}
