"use client";

import {
  getListTenantsQueryOptions,
  ListTenantsSort,
  schemas,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Freshness } from "@repo/ui/components/freshness";
import { Icon, PlusIcon } from "@repo/ui/components/icon";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Tabs } from "@repo/ui/components/tabs";
import { Input as InputBase } from "@repo/ui/components/ui/input";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useCallback, useEffect, useState } from "react";
import { ListError } from "@/shared/list-error";
import { CreateOrganisationDialog } from "./create-organisation-dialog";
import { useOrganisationActions } from "./organisation-actions";
import { OrganisationsTable } from "./organisations-table";
import { useDates } from "./use-dates";
import { searchDelay, type Tab, tabs, useListParams } from "./use-list-params";

const all = "all";

/** The tab's label key: All, then the statuses (the Main board). */
const tabLabels = {
  active: "organisations.tabActive",
  suspended: "organisations.tabSuspended",
  closed: "organisations.tabClosed",
  purged: "organisations.tabDeleted",
} as const satisfies Record<Tab, string>;

/** The title of an empty tab (the ListStates board). */
const noMatchLabels = {
  active: "organisations.noMatchActive",
  suspended: "organisations.noMatchSuspended",
  closed: "organisations.noMatchClosed",
  purged: "organisations.noMatchPurged",
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
          error={list.error}
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

/** The search field: it sends the text 300 ms after the last key, and follows the URL on back and forward. */
function Search({
  value,
  label,
  onSearch,
}: {
  value: string;
  label: string;
  onSearch: (q: string) => void;
}) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  useEffect(() => {
    if (draft === value) return;
    const timer = setTimeout(() => onSearch(draft), searchDelay);
    return () => clearTimeout(timer);
  }, [draft, value, onSearch]);
  return (
    <InputBase
      type="search"
      aria-label={label}
      placeholder={label}
      maxLength={schemas.listTenantsQueryQMax}
      value={draft}
      onChange={(event) => setDraft(event.target.value)}
      className="h-control-md w-full rounded-md bg-card px-3 text-body pointer-coarse:h-control-lg tablet:w-64"
    />
  );
}
