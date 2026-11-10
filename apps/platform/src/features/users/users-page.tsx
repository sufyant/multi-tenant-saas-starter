"use client";

import {
  getListUsersInfiniteQueryOptions,
  ListUsersSort,
  problemOf,
  schemas,
  type UserListPageResponse,
  UserResponseStatus,
} from "@repo/api-client";
import { brand } from "@repo/brand";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { ListError } from "@repo/ui/components/list-error";
import { LoadMore } from "@repo/ui/components/load-more";
import { Search } from "@repo/ui/components/search";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Tabs } from "@repo/ui/components/tabs";
import { nextPage, pageSize } from "@repo/ui/lib/pages";
import { keepPreviousData, useInfiniteQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useCallback } from "react";
import { leaveOf } from "@/shared/problem-screen";
import { useNextPage } from "@/shared/use-next-page";
import { type Tab, tabs, useListParams } from "./use-list-params";
import { useUserActions } from "./use-user-actions";
import { UsersTable } from "./users-table";

const all = "all";

/** The tab's label key: All, then the statuses (5.01). */
const tabLabels = {
  [UserResponseStatus.Active]: "users.tabActive",
  [UserResponseStatus.Suspended]: "users.tabSuspended",
} as const satisfies Record<Tab, string>;

/** The title of an empty tab (the ListStates board). */
const noMatchLabels = {
  [UserResponseStatus.Active]: "users.noMatchActive",
  [UserResponseStatus.Suspended]: "users.noMatchSuspended",
} as const satisfies Record<Tab, string>;

/** A tab's count: its status's, and All their sum, a deleted user included (`statusCounts`, 37b-backend). */
const countOf = (
  counts: UserListPageResponse["statusCounts"] | undefined,
  tab: Tab | typeof all,
) => {
  if (!counts) return undefined;
  if (tab === all) return counts.active + counts.suspended + counts.deleted;
  return counts[tab];
};

/**
 * Users, the people of every organisation (5.01 to 5.04, 5.16, 5.18, 5.19): the frame's description and no Freshness (F59), the
 * status tabs with their counts, the search by name or email, and the table. The list grows with Load more (F9); the URL holds the
 * tab, the search and the sort, never the page. The System admin label is information; the System admins page manages it.
 */
export function UsersPage() {
  const t = useTranslations();
  const [params, setParams] = useListParams();
  const actions = useUserActions();
  const { status, q, sort } = params;
  const onSearch = useCallback(
    (next: string) => void setParams({ q: next || null }),
    [setParams],
  );

  const list = useInfiniteQuery(
    getListUsersInfiniteQueryOptions(
      {
        q: q || undefined,
        status: status ? [status] : undefined,
        pageSize,
        sort: sort ?? undefined,
      },
      {
        query: {
          initialPageParam: 1,
          getNextPageParam: nextPage,
          placeholderData: keepPreviousData,
        },
      },
    ),
  );
  const next = useNextPage(list, (count) => t("users.loadedUsers", { count }));
  const counts = list.data?.pages[0]?.statusCounts;

  const body = (() => {
    if (list.isPending) return <Skeleton rows={8} label={t("users.loading")} />;
    if (!list.data)
      return (
        <ListError
          title={t("users.error")}
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    const rows = list.data.pages.flatMap((page) => page.items);
    // There is always a root system admin, so only a search or a tab finds nobody (5.03).
    if (rows.length === 0 && q)
      return (
        <EmptyState
          kind="no-results"
          title={t("users.noMatchSearch", { q })}
          description={t("users.noMatchSearchText")}
          action={
            <Button onClick={() => void setParams({ q: null })}>
              {t("users.clearSearch")}
            </Button>
          }
        />
      );
    if (rows.length === 0 && status)
      return (
        <EmptyState
          kind="no-results"
          title={t(noMatchLabels[status])}
          description={t("users.noMatchText")}
          action={
            <Button onClick={() => void setParams({ status: null })}>
              {t("users.clearFilters")}
            </Button>
          }
        />
      );
    return (
      <div className="flex flex-col gap-2">
        <UsersTable
          rows={rows}
          sort={sort}
          actions={actions}
          onSortChange={(value) =>
            void setParams({
              sort:
                Object.values(ListUsersSort).find(
                  (sorted) => sorted === value,
                ) ?? null,
            })
          }
        />
        <LoadMore
          hasMore={list.hasNextPage}
          loading={list.isFetchingNextPage}
          onMore={() => void next.more()}
          label={t("users.loadMore")}
          loadingLabel={t("users.loadingMore")}
          announcement={next.announcement}
          failed={
            next.failed
              ? { text: t("users.moreError"), retryLabel: t("common.tryAgain") }
              : undefined
          }
        />
      </div>
    );
  })();

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      {/* On a phone the top bar names the page (5.16), so its header stays for screen readers. */}
      <div className="sr-only flex min-w-0 flex-col gap-0.5 tablet:not-sr-only">
        <h1 className="text-page-title">{t("users.title")}</h1>
        <p className="text-body text-muted-foreground">
          {t("users.intro", { product: brand.name })}
        </p>
      </div>
      <Tabs
        label={t("users.tabs")}
        current={status ?? all}
        items={[
          { id: all, label: t("users.tabAll"), count: countOf(counts, all) },
          ...tabs.map((tab) => ({
            id: tab,
            label: t(tabLabels[tab]),
            count: countOf(counts, tab),
          })),
        ]}
        onValueChange={(value) =>
          void setParams({ status: tabs.find((tab) => tab === value) ?? null })
        }
      />
      <Search
        value={q}
        label={t("users.search")}
        maxLength={schemas.listUsersQueryQMax}
        onSearch={onSearch}
      />
      {body}
      {actions.dialog}
    </section>
  );
}
