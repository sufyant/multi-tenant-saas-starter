"use client";

import {
  getListTenantsInfiniteQueryOptions,
  ListTenantsSort,
  problemOf,
  schemas,
  TenantResponseStatus,
} from "@repo/api-client";
import { brand } from "@repo/brand";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Icon, PlusIcon } from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { LoadMore } from "@repo/ui/components/load-more";
import { Search } from "@repo/ui/components/search";
import { Skeleton } from "@repo/ui/components/skeleton";
import { nextPage, pageSize } from "@repo/ui/lib/pages";
import { keepPreviousData, useInfiniteQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useCallback, useState } from "react";
import { useLoadMore } from "@/shared/load-more";
import { leaveOf } from "@/shared/problem-screen";
import { CreateOrganisationDialog } from "./create-organisation-dialog";
import { OrganisationsTable } from "./organisations-table";
import {
  countOf,
  statusesOf,
  type Tab,
  tabs,
  useListParams,
} from "./use-list-params";
import { useOrganisationActions } from "./use-organisation-actions";
import { ViewChoice } from "./view-choice";

const all = "all";

/** The tab's label key: All, then the statuses (4.01). */
const tabLabels = {
  [TenantResponseStatus.Active]: "organisations.tabActive",
  [TenantResponseStatus.Suspended]: "organisations.tabSuspended",
  [TenantResponseStatus.Closed]: "organisations.tabClosed",
  [TenantResponseStatus.Purged]: "organisations.tabDeleted",
} as const satisfies Record<Tab, string>;

/** The title of an empty tab. */
const noMatchLabels = {
  [TenantResponseStatus.Active]: "organisations.noMatchActive",
  [TenantResponseStatus.Suspended]: "organisations.noMatchSuspended",
  [TenantResponseStatus.Closed]: "organisations.noMatchClosed",
  [TenantResponseStatus.Purged]: "organisations.noMatchPurged",
} as const satisfies Record<Tab, string>;

/**
 * Organisations, the platform's start page (4.01 to 4.06, 4.43): the title with its description and no Freshness (F59), Create
 * organisation, the status tabs with the counts of `ListTenants`, the search, the table, and Load more (F9). The tabs, the search and
 * the sort are in the URL, never the page. On a phone the top bar names the page, so its title stays for screen readers; Create spans
 * the width, and the tabs are one choice.
 */
export function OrganisationsPage() {
  const t = useTranslations();
  const [params, setParams] = useListParams();
  const [creating, setCreating] = useState(false);
  const actions = useOrganisationActions();
  const { status, q, sort } = params;
  const onSearch = useCallback(
    (next: string) => void setParams({ q: next || null }),
    [setParams],
  );

  const list = useInfiniteQuery(
    getListTenantsInfiniteQueryOptions(
      {
        q: q || undefined,
        status: statusesOf(status),
        sort: sort ?? undefined,
        pageSize,
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
  const { announcement, loadMore } = useLoadMore(list, {
    loaded: (count) => t("organisations.loadedMore", { count }),
    failed: t("organisations.moreError"),
  });
  const counts = list.data?.pages[0]?.statusCounts;

  const create = (
    <Button
      variant="primary"
      icon={<Icon icon={PlusIcon} />}
      onClick={() => setCreating(true)}
      className="max-tablet:h-control-lg max-tablet:w-full"
    >
      {t("organisations.create")}
    </Button>
  );

  const body = (() => {
    if (list.isPending)
      return <Skeleton rows={6} label={t("organisations.loading")} />;
    if (!list.data)
      return (
        <ListError
          title={t("organisations.error")}
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    const rows = list.data.pages.flatMap((page) => page.items);
    if (rows.length === 0 && q)
      return (
        <EmptyState
          kind="no-results"
          title={t("organisations.noMatchSearch", { q })}
          description={t("organisations.noMatchText")}
          action={
            <Button onClick={() => void setParams({ q: null })}>
              {t("organisations.clearSearch")}
            </Button>
          }
        />
      );
    if (rows.length === 0 && status)
      return (
        <EmptyState
          kind="no-results"
          title={t(noMatchLabels[status])}
          action={
            <Button onClick={() => void setParams({ status: null })}>
              {t("organisations.clearFilters")}
            </Button>
          }
        />
      );
    if (rows.length === 0)
      return (
        <EmptyState
          title={t("organisations.emptyTitle")}
          description={t("organisations.emptyText")}
          action={
            <Button
              icon={<Icon icon={PlusIcon} />}
              onClick={() => setCreating(true)}
            >
              {t("organisations.create")}
            </Button>
          }
        />
      );
    return (
      <div className="flex flex-col gap-2">
        <OrganisationsTable
          rows={rows}
          sort={sort}
          actions={actions}
          onSortChange={(next) =>
            void setParams({
              sort:
                Object.values(ListTenantsSort).find(
                  (value) => value === next,
                ) ?? null,
            })
          }
        />
        <LoadMore
          hasMore={list.hasNextPage}
          loading={list.isFetchingNextPage}
          onMore={loadMore}
          label={t("organisations.loadMore")}
          loadingLabel={t("organisations.loadingMore")}
          announcement={announcement}
        />
      </div>
    );
  })();

  // The first run shows the header and the empty state only (4.06).
  const firstRun =
    !q &&
    !status &&
    list.data?.pages[0]?.items.length === 0 &&
    counts &&
    countOf(counts, null) === 0;

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <div className="flex flex-col gap-4 tablet:flex-row tablet:items-center tablet:justify-between">
        <div className="sr-only flex min-w-0 flex-col gap-0.5 tablet:not-sr-only">
          <h1 className="text-page-title">{t("organisations.title")}</h1>
          <p className="text-body text-muted-foreground">
            {t("organisations.intro", { brand: brand.name })}
          </p>
        </div>
        {create}
      </div>
      {!firstRun && (
        <>
          <ViewChoice
            label={t("organisations.tabs")}
            current={status ?? all}
            items={[
              {
                id: all,
                label: t("organisations.tabAll"),
                count: counts && countOf(counts, null),
              },
              ...tabs.map((tab) => ({
                id: tab,
                label: t(tabLabels[tab]),
                count: counts && countOf(counts, tab),
              })),
            ]}
            onValueChange={(next) =>
              void setParams({
                status: tabs.find((tab) => tab === next) ?? null,
              })
            }
          />
          <Search
            value={q}
            label={t("organisations.search")}
            maxLength={schemas.listTenantsQueryQMax}
            onSearch={onSearch}
          />
        </>
      )}
      {body}
      {creating && (
        <CreateOrganisationDialog onClose={() => setCreating(false)} />
      )}
      {actions.dialog}
    </section>
  );
}
