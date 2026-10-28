"use client";

import {
  type ExcessResponse,
  type GetPlanUpdatePreviewRequestEntitlements,
  getPlanUpdatePreview,
  type ListPageOfTenantExcessResponse,
  type PlanResponse,
  problemOf,
  schemas,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Dialog } from "@repo/ui/components/dialog";
import { Search } from "@repo/ui/components/search";
import { Status } from "@repo/ui/components/status";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useInfiniteQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { isFeatureKey, useEntitlementText } from "@/shared/entitlements";

type Row = ExcessResponse & { id: string; tenantId: string; name: string };

/**
 * The impact of a plan edit before it is saved (the PlanImpact board): the organisations on the plan that the edit puts over a
 * limit, each with the key, its count, the new value and how far over; a feature turned off lists what uses it. The list is the
 * API's page (K5): searched by name or slug, the furthest over first, with "Load more" and the total. Each organisation opens in
 * a new tab, so the edit is not lost. It never blocks: Cancel or Save anyway.
 */
export function ImpactDialog({
  plan,
  entitlements,
  first,
  pending,
  onCancel,
  onConfirm,
}: {
  plan: Pick<PlanResponse, "id" | "name">;
  entitlements: GetPlanUpdatePreviewRequestEntitlements;
  /** The first page, which the save asked before it opened the dialog. */
  first: ListPageOfTenantExcessResponse;
  pending: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useTranslations("plans");
  const common = useTranslations("common");
  const text = useEntitlementText();
  const failureText = useFailureText();
  const [q, setQ] = useState("");
  const pages = useInfiniteQuery({
    queryKey: ["plan-update-preview", plan.id, entitlements, q],
    queryFn: ({ pageParam }) =>
      getPlanUpdatePreview(
        plan.id,
        { entitlements },
        { page: pageParam, ...(q ? { q } : {}) },
      ),
    initialPageParam: 1,
    getNextPageParam: (last, all) =>
      all.reduce((count, page) => count + page.items.length, 0) < last.total
        ? last.page + 1
        : undefined,
    ...(q === "" ? { initialData: { pages: [first], pageParams: [1] } } : {}),
    staleTime: Number.POSITIVE_INFINITY,
  });
  const loaded = pages.data?.pages ?? [];
  const total = loaded.at(-1)?.total ?? first.total;
  const tenants = loaded.flatMap((page) => page.items);
  const rows: Row[] = tenants.flatMap((tenant) =>
    tenant.excesses.map((excess) => ({
      ...excess,
      id: `${tenant.tenantId}-${excess.key}`,
      tenantId: tenant.tenantId,
      name: tenant.name,
    })),
  );

  const columns: TableColumn<Row>[] = [
    {
      key: "organisation",
      header: t("organisation"),
      render: (row) => (
        <a
          href={`/organisations/${row.tenantId}`}
          target="_blank"
          rel="noopener"
          className="text-primary underline underline-offset-4 hover:text-primary-hover"
        >
          <bdi className="font-medium">{row.name}</bdi>
          <span className="sr-only"> {t("newTab")}</span>
        </a>
      ),
    },
    {
      key: "key",
      header: t("limit"),
      render: (row) => text.name(row.key),
    },
    {
      key: "count",
      header: t("now"),
      align: "end",
      render: (row) => (
        <span className="inline-flex flex-wrap items-center justify-end gap-2">
          <span className="tabular-nums">{row.count}</span>
          {!isFeatureKey(row.key) && (
            <Status tone="warning">{t("overBy", { count: row.amount })}</Status>
          )}
        </span>
      ),
    },
    {
      key: "newValue",
      header: t("newLimit"),
      align: "end",
      card: "end",
      render: (row) => text.label(isFeatureKey(row.key), row.newValue),
    },
  ];

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onCancel()}
      title={t("impactTitle", { plan: plan.name })}
      description={t("impactText", { count: first.total })}
      actionLabel={t("saveAnyway")}
      cancelLabel={common("cancel")}
      pending={pending}
      onConfirm={onConfirm}
    >
      <div className="flex flex-col gap-3">
        <Search
          value={q}
          label={t("search")}
          maxLength={schemas.getPlanUpdatePreviewQueryQMax}
          onSearch={setQ}
        />
        {pages.isError ? (
          <Banner tone="destructive" title={t("previewError")}>
            {failureText(problemOf(pages.error))}
          </Banner>
        ) : (
          <Table
            columns={columns}
            rows={rows}
            rowKey="id"
            label={t("impactTitle", { plan: plan.name })}
            rowCount={rows.length}
            page={1}
            pageSize={Math.max(rows.length, 1)}
            pager={{
              previous: common("previous"),
              next: common("next"),
              page: (current, count) =>
                common("page", { page: current, count }),
            }}
          />
        )}
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-small text-muted-foreground">
            {t("shown", { shown: tenants.length, total })}
          </p>
          {pages.hasNextPage && (
            <Button
              disabled={pages.isFetchingNextPage}
              onClick={() => void pages.fetchNextPage()}
            >
              {t("loadMore")}
            </Button>
          )}
        </div>
      </div>
    </Dialog>
  );
}
