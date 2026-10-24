"use client";

import {
  getListTenantsQueryOptions,
  ListTenantsSort,
  problemOf,
  schemas,
  TenantResponseStatus,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Freshness } from "@repo/ui/components/freshness";
import { Icon, PlusIcon } from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { Search } from "@repo/ui/components/search";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Tabs } from "@repo/ui/components/tabs";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useCallback, useState } from "react";
import { leaveOf } from "@/shared/problem-screen";
import { useDates } from "@/shared/use-dates";
import { CreateOrganisationDialog } from "./create-organisation-dialog";
import { OrganisationsTable } from "./organisations-table";
import { type Tab, tabs, useListParams } from "./use-list-params";
import { useOrganisationActions } from "./use-organisation-actions";

const all = "all";

/** The tab's label key: All, then the statuses (the Main board). */
const tabLabels = {
  [TenantResponseStatus.Active]: "organisations.tabActive",
  [TenantResponseStatus.Suspended]: "organisations.tabSuspended",
  [TenantResponseStatus.Closed]: "organisations.tabClosed",
  [TenantResponseStatus.Purged]: "organisations.tabDeleted",
} as const satisfies Record<Tab, string>;

/** The title of an empty tab (the ListStates board). */
const noMatchLabels = {
  [TenantResponseStatus.Active]: "organisations.noMatchActive",
  [TenantResponseStatus.Suspended]: "organisations.noMatchSuspended",
  [TenantResponseStatus.Closed]: "organisations.noMatchClosed",
  [TenantResponseStatus.Purged]: "organisations.noMatchPurged",
} as const satisfies Record<Tab, string>;

/**
 * Organisations, the platform's start page (the Main, OrgsFiltered, OrgsEmpty and ListStates boards): the status tabs, the
 * search, the table and Create organisation. Every parameter of the list is in the URL (F9).
 */
export function OrganisationsPage() {
  const t = useTranslations();
  const { timeZone } = useDates();
  const [params, setParams] = useListParams();
  const [creating, setCreating] = useState(false);
  const actions = useOrganisationActions();
  const { status, q, page, sort } = params;
  const onSearch = useCallback(
    (next: string) => void setParams({ q: next || null, page: null }),
    [setParams],
  );

  const list = useQuery(
    getListTenantsQueryOptions(
      {
        q: q || undefined,
        status: status ?? undefined,
        page,
        sort: sort ?? undefined,
      },
      { query: { placeholderData: keepPreviousData } },
    ),
  );

  const filtered = Boolean(q || status);
  const clear = () => void setParams({ q: null, status: null, page: null });
  const create = (
    <Button
      variant="primary"
      icon={<Icon icon={PlusIcon} />}
      onClick={() => setCreating(true)}
    >
      {t("organisations.create")}
    </Button>
  );

  const body = (() => {
    if (list.isPending)
      return <Skeleton rows={4} label={t("organisations.loading")} />;
    if (list.isError)
      return (
        <ListError
          title={t("organisations.error")}
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    if (list.data.items.length === 0 && filtered)
      return (
        <EmptyState
          kind="no-results"
          title={
            q
              ? t("organisations.noMatchSearch", { q })
              : status && t(noMatchLabels[status])
          }
          description={t("organisations.noMatchText")}
          action={
            <Button onClick={clear}>{t("organisations.clearFilters")}</Button>
          }
        />
      );
    if (list.data.items.length === 0)
      return (
        <EmptyState
          title={t("organisations.emptyTitle")}
          description={t("organisations.emptyText")}
          action={create}
        />
      );
    return (
      <OrganisationsTable
        page={list.data}
        sort={sort}
        timeZone={timeZone}
        actions={actions}
        onSortChange={(next) =>
          void setParams({
            sort:
              Object.values(ListTenantsSort).find((value) => value === next) ??
              null,
            page: null,
          })
        }
        onPageChange={(next) => void setParams({ page: next })}
      />
    );
  })();

  // The first run shows only the title and the empty state (the OrgsEmpty board).
  const firstRun = !filtered && list.data?.total === 0;

  return (
    <section className="flex min-w-0 max-w-[1160px] flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-page-title">{t("organisations.title")}</h1>
          {list.dataUpdatedAt > 0 && (
            <Freshness
              at={new Date(list.dataUpdatedAt)}
              locale={t("locale.format")}
              timeZone={timeZone}
              label={t("common.updated")}
            />
          )}
        </div>
        {!firstRun && create}
      </div>
      {!firstRun && (
        <div className="flex flex-wrap items-end justify-between gap-4">
          <Tabs
            label={t("organisations.tabs")}
            current={status ?? all}
            items={[
              { id: all, label: t("organisations.tabAll") },
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
            label={t("organisations.search")}
            maxLength={schemas.listTenantsQueryQMax}
            onSearch={onSearch}
          />
        </div>
      )}
      {body}
      {creating && (
        <CreateOrganisationDialog onClose={() => setCreating(false)} />
      )}
      {actions.dialog}
    </section>
  );
}
