"use client";

import {
  getListPlansInfiniteQueryOptions,
  ListPlansSort,
  type PlanListItemResponse,
  problemOf,
} from "@repo/api-client";
import { Icon, PlusIcon } from "@repo/ui/components/icon";
import { Label } from "@repo/ui/components/label";
import { ListError } from "@repo/ui/components/list-error";
import { LoadMore } from "@repo/ui/components/load-more";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { buttonVariants } from "@repo/ui/components/ui/button";
import { nextPage, pageSize } from "@repo/ui/lib/pages";
import { cn } from "@repo/ui/lib/utils";
import { useInfiniteQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useLoadMore } from "@/shared/load-more";
import { planPage, plansPage } from "@/shared/plans";
import { leaveOf } from "@/shared/problem-screen";
import { TextLink } from "@/shared/text-link";
import { useDates } from "@/shared/use-dates";

/**
 * Plans (6.01 to 6.03, 6.17): each plan with its Default label, the organisations on it and when it was created, under the
 * frame's description and no Freshness (F59). The list grows with Load more (F9). There is no empty state: a default plan always
 * exists. On a phone the top bar names the page, so its title stays for screen readers, and New plan spans the width. Every system
 * admin holds every platform permission (the API's A4), so nothing checks one; the API still decides.
 */
export function PlansPage() {
  const t = useTranslations();
  const dates = useDates();
  const list = useInfiniteQuery(
    getListPlansInfiniteQueryOptions(
      { pageSize, sort: ListPlansSort.Name },
      { query: { initialPageParam: 1, getNextPageParam: nextPage } },
    ),
  );
  const { announcement, loadMore } = useLoadMore(list, {
    loaded: (count) => t("plans.loadedPlans", { count }),
    failed: t("plans.moreError"),
  });

  const columns: TableColumn<PlanListItemResponse>[] = [
    {
      key: "name",
      header: t("plans.nameColumn"),
      render: (plan) => (
        <span className="flex min-w-0 flex-wrap items-center gap-2">
          <TextLink href={planPage(plan.id)}>
            <bdi className="font-medium">{plan.name}</bdi>
          </TextLink>
          {plan.isDefault && <Label>{t("plans.default")}</Label>}
        </span>
      ),
    },
    {
      key: "tenantCount",
      header: t("plans.organisationsColumn"),
      width: "w-38.5",
      render: (plan) => plan.tenantCount,
      cardRender: (plan) =>
        t("plans.organisations", { count: plan.tenantCount }),
    },
    {
      key: "createdAt",
      header: t("plans.createdColumn"),
      card: "hide",
      render: (plan) => (
        <time dateTime={new Date(plan.createdAt).toISOString()}>
          {dates.date(plan.createdAt)}
        </time>
      ),
    },
  ];

  const body = (() => {
    if (list.isPending) return <Skeleton rows={3} label={t("plans.loading")} />;
    if (!list.data)
      return (
        <ListError
          title={t("plans.error")}
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    const rows = list.data.pages.flatMap((page) => page.items);
    return (
      <div className="flex flex-col gap-2">
        <Table
          columns={columns}
          rows={rows}
          rowKey="id"
          label={t("plans.title")}
          rowCount={rows.length}
          page={1}
          pageSize={Math.max(rows.length, 1)}
          pager={{
            previous: t("common.previous"),
            next: t("common.next"),
            page: (current, count) =>
              t("common.page", { page: current, count }),
          }}
        />
        <LoadMore
          hasMore={list.hasNextPage}
          loading={list.isFetchingNextPage}
          onMore={loadMore}
          label={t("plans.loadMore")}
          loadingLabel={t("plans.loadingMore")}
          announcement={announcement}
        />
      </div>
    );
  })();

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <div className="flex flex-col gap-4 tablet:flex-row tablet:items-start tablet:justify-between">
        <div className="sr-only flex min-w-0 flex-col gap-0.5 tablet:not-sr-only">
          <h1 className="text-page-title">{t("plans.title")}</h1>
          <p className="max-w-[720px] text-body text-muted-foreground">
            {t("plans.intro")}
          </p>
        </div>
        {/* A link to a page, drawn as the primary button: a link, not a button, because it navigates. */}
        <Link
          href={`${plansPage}/new`}
          className={cn(
            buttonVariants({ variant: "default" }),
            "max-tablet:h-control-lg max-tablet:w-full",
          )}
        >
          <Icon icon={PlusIcon} />
          {t("plans.newPlan")}
        </Link>
      </div>
      {body}
    </section>
  );
}
