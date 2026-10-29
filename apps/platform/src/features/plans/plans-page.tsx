"use client";

import {
  getListPlansQueryOptions,
  type PlanListItemResponse,
  problemOf,
} from "@repo/api-client";
import { Freshness } from "@repo/ui/components/freshness";
import { Icon, PlusIcon } from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { Tag } from "@repo/ui/components/tag";
import { buttonVariants } from "@repo/ui/components/ui/button";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { planPage, plansQuery } from "@/shared/plans";
import { leaveOf } from "@/shared/problem-screen";
import { SettingsNav, settingsPages } from "@/shared/settings-nav";
import { TextLink } from "@/shared/text-link";
import { useDates } from "@/shared/use-dates";

/**
 * Settings › Plans (the PlansList board): each plan with its Default tag and the organisations on it, and New plan. There is no
 * empty state: a default plan always exists. Every system admin holds every platform permission (the API's A4), so nothing
 * checks one; the API still decides.
 */
export function PlansPage() {
  const t = useTranslations();
  const { timeZone } = useDates();
  const list = useQuery(
    getListPlansQueryOptions(plansQuery, {
      query: { placeholderData: keepPreviousData },
    }),
  );
  const locale = t("locale.format");

  const columns: TableColumn<PlanListItemResponse>[] = [
    {
      key: "name",
      header: t("plans.nameColumn"),
      render: (plan) => (
        <span className="flex min-w-0 flex-wrap items-center gap-2">
          <TextLink href={planPage(plan.id)}>
            <bdi className="font-medium">{plan.name}</bdi>
          </TextLink>
          {plan.isDefault && <Tag>{t("plans.default")}</Tag>}
        </span>
      ),
    },
    {
      key: "tenantCount",
      header: t("plans.organisationsColumn"),
      align: "end",
      card: "end",
      render: (plan) => plan.tenantCount,
    },
  ];

  const body = (() => {
    if (list.isPending) return <Skeleton rows={3} label={t("plans.loading")} />;
    if (list.isError)
      return (
        <ListError
          title={t("plans.error")}
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
        label={t("plans.title")}
        rowCount={list.data.total}
        page={list.data.page}
        pageSize={list.data.pageSize}
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
      <SettingsNav current="plans" />
      <section className="flex min-w-0 max-w-[1160px] flex-1 flex-col gap-4">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="text-page-title">{t("plans.title")}</h1>
            {list.dataUpdatedAt > 0 && (
              <Freshness
                at={new Date(list.dataUpdatedAt)}
                locale={locale}
                timeZone={timeZone}
                label={t("common.updated")}
              />
            )}
          </div>
          {/* A link to a page, drawn as the primary button: a link, not a button, because it navigates. */}
          <Link
            href={`${settingsPages.plans}/new`}
            className={buttonVariants({ variant: "default" })}
          >
            <Icon icon={PlusIcon} />
            {t("plans.newPlan")}
          </Link>
        </div>
        <p className="max-w-[720px] text-body text-muted-foreground">
          {t("plans.intro")}
        </p>
        {body}
      </section>
    </div>
  );
}
