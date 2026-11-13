"use client";

import {
  type ExcessResponse,
  type GetPlanUpdatePreviewRequestEntitlements,
  getPlanUpdatePreview,
  type PlanResponse,
  type PlanUpdatePreviewPageResponse,
  problemOf,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Dialog } from "@repo/ui/components/dialog";
import { Pill } from "@repo/ui/components/pill";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useInfiniteQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { isFeatureKey, useEntitlementText } from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";

type Row = ExcessResponse & { id: string; tenantId: string; name: string };

/**
 * The impact of a plan edit before it is saved (the PlanImpact board): the organisations on the plan that the edit puts over a
 * limit, each with the key, its count, the new value and how far over; a feature turned off lists what uses it. The list is the
 * API's page (K5): the furthest over first, with "Load more" and the total, and no search, as 6.07 draws it. Each organisation
 * opens in a new tab, so the edit is not lost. It never blocks: Cancel or Save anyway.
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
  first: PlanUpdatePreviewPageResponse;
  pending: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const t = useTranslations("plans");
  const common = useTranslations("common");
  const text = useEntitlementText();
  const failureText = useFailureText();
  const pages = useInfiniteQuery({
    // A POST that reads: its key is its operation and its arguments, as a generated key is (F2, F44).
    queryKey: ["getPlanUpdatePreview", plan.id, { entitlements }],
    queryFn: ({ pageParam }) =>
      getPlanUpdatePreview(plan.id, { entitlements }, { page: pageParam }),
    initialPageParam: 1,
    getNextPageParam: (last, all) =>
      all.reduce((count, page) => count + page.items.length, 0) < last.total
        ? last.page + 1
        : undefined,
    initialData: { pages: [first], pageParams: [1] },
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

  // The organisations whose Activity log the edit shortens (6.19); a count of 0 shortens nothing (#271).
  const shortened = first.retentionShortened?.count
    ? first.retentionShortened
    : null;
  const shortenedText =
    shortened &&
    t("retentionText", {
      ...text.periods,
      count: shortened.count,
      from: shortened.fromDays === null ? "none" : String(shortened.fromDays),
      to: text.days(shortened.toDays),
      toCount: shortened.toDays,
    });
  const overText = t("impactText", { count: first.total });

  const columns: TableColumn<Row>[] = [
    {
      key: "organisation",
      header: t("organisation"),
      render: (row) => (
        <a
          href={`/organisations/${row.tenantId}`}
          target="_blank"
          rel="noopener"
          className="text-link underline underline-offset-4"
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
            <Pill tone="warning">{t("overBy", { count: row.amount })}</Pill>
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
      size="wide"
      open
      onOpenChange={(open) => !open && onCancel()}
      title={t.rich("impactTitle", { plan: plan.name, ...isolated })}
      description={shortenedText || overText}
      actionLabel={t("saveAnyway")}
      cancelLabel={common("cancel")}
      pending={pending}
      onConfirm={onConfirm}
    >
      {total > 0 && (
        <div className="flex flex-col gap-3">
          {shortenedText && (
            <p className="text-body text-muted-foreground">{overText}</p>
          )}
          {pages.isError ? (
            <Banner tone="destructive" title={t("previewError")}>
              {failureText(problemOf(pages.error))}
            </Banner>
          ) : (
            <Table
              columns={columns}
              rows={rows}
              rowKey="id"
              label={t.markup("impactTitle", {
                plan: plan.name,
                bdi: (chunks) => chunks,
              })}
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
      )}
    </Dialog>
  );
}
