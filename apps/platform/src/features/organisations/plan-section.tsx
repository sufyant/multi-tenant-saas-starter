"use client";

import {
  EntitlementOverrideResponseState,
  getGetTenantPlanQueryOptions,
  problemOf,
  type TenantDetailResponse,
  type TenantEntitlementResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Icon, PlusIcon } from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Status } from "@repo/ui/components/status";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { Time } from "@repo/ui/components/time";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useEntitlementText } from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";
import { nameOf } from "@/shared/name-of";
import { leaveOf } from "@/shared/problem-screen";
import { useDates } from "@/shared/use-dates";
import { ChangePlanDialog } from "./change-plan-dialog";
import { OverrideDialog } from "./override-dialog";
import { RemoveOverrideDialog } from "./remove-override-dialog";

/** How far over its limit the organisation is: its count less the value that applies, for a limit with a number. */
export const overBy = (row: TenantEntitlementResponse) =>
  !row.isFeature && !row.effective.unlimited
    ? Math.max(0, row.count - (row.effective.maximum ?? 0))
    : 0;

const isExpired = (row: TenantEntitlementResponse) =>
  row.override?.state === EntitlementOverrideResponseState.Expired;

type Open =
  | { dialog: "override"; key: string | null }
  | { dialog: "remove"; row: TenantEntitlementResponse }
  | { dialog: "change" };

/**
 * An organisation's Plan section (the OrgPlan boards): the plan and Change plan, then a row per key with the plan's value, the
 * override (value, expiry, reason, who set it), the value that applies, and for a limit the count. An expired override stays
 * listed, crossed out, until someone edits or removes it. A closed or purging organisation's plan and overrides are read-only
 * (409 `tenant.not_active`); a suspended one's can change. Every system admin holds every platform permission (the API's A4).
 */
export function PlanSection({
  organisation,
}: {
  organisation: TenantDetailResponse;
}) {
  const t = useTranslations();
  const text = useEntitlementText();
  const { timeZone } = useDates();
  const locale = t("locale.format");
  const [open, setOpen] = useState<Open | null>(null);
  const plan = useQuery(getGetTenantPlanQueryOptions(organisation.id));
  const readOnly =
    organisation.status === TenantResponseStatus.Closed ||
    organisation.status === TenantResponseStatus.Purging;
  const name = organisation.name;

  const columns: TableColumn<TenantEntitlementResponse>[] = [
    {
      key: "key",
      header: t("planSection.key"),
      render: (row) => (
        <span className="flex min-w-0 flex-col">
          <span className="font-medium">{text.name(row.key)}</span>
          <code dir="ltr" className="font-mono text-mono text-muted-foreground">
            {row.key}
          </code>
        </span>
      ),
      cardRender: (row) => text.name(row.key),
    },
    {
      key: "plan",
      header: t("planSection.plan"),
      render: (row) => text.label(row.isFeature, row.planValue),
    },
    {
      key: "override",
      header: t("planSection.override"),
      render: (row) => {
        const override = row.override;
        if (!override) return <span className="text-muted-foreground">—</span>;
        const value = text.label(row.isFeature, override.value);
        return (
          <span className="flex min-w-0 flex-col gap-0.5">
            <span className="flex flex-wrap items-center gap-2">
              {isExpired(row) ? (
                <del className="text-muted-foreground">{value}</del>
              ) : (
                <span className="font-medium">{value}</span>
              )}
              {isExpired(row) && (
                <Status tone="neutral">{t("planSection.expired")}</Status>
              )}
            </span>
            <span className="text-small text-muted-foreground">
              {override.expiresAt
                ? t.rich("planSection.until", {
                    // The Time component: the zone and UTC are one hover away (F18).
                    date: () => (
                      <Time
                        at={override.expiresAt ?? ""}
                        show="date"
                        locale={locale}
                        timeZone={timeZone}
                      />
                    ),
                  })
                : t("planSection.noExpiry")}
            </span>
            <bdi className="text-small">{override.reason}</bdi>
            <span className="text-small text-muted-foreground">
              {t("planSection.setBy", {
                name: override.setBy.deleted
                  ? t("activity.deletedUser")
                  : (nameOf(override.setBy) ?? t("activity.unnamedUser")),
              })}
            </span>
          </span>
        );
      },
      cardRender: (row) =>
        row.override
          ? `${t("planSection.override")}: ${text.label(row.isFeature, row.override.value)}${isExpired(row) ? ` (${t("planSection.expired")})` : ""}`
          : null,
    },
    {
      key: "effective",
      header: t("planSection.applies"),
      card: "end",
      render: (row) => (
        <strong>{text.label(row.isFeature, row.effective)}</strong>
      ),
    },
    {
      key: "used",
      header: t("planSection.used"),
      align: "end",
      render: (row) =>
        row.isFeature ? (
          <span className="text-muted-foreground">—</span>
        ) : (
          <span className="inline-flex flex-wrap items-center justify-end gap-2">
            <span className="tabular-nums">{row.count}</span>
            {overBy(row) > 0 && (
              <Status tone="warning">
                {t("planSection.overBy", { count: overBy(row) })}
              </Status>
            )}
          </span>
        ),
      cardRender: (row) =>
        row.isFeature
          ? null
          : `${t("planSection.used")}: ${row.count}${overBy(row) > 0 ? ` · ${t("planSection.overBy", { count: overBy(row) })}` : ""}`,
    },
  ];

  const actions = readOnly
    ? undefined
    : (row: TenantEntitlementResponse) => (
        <RowMenu
          label={t("planSection.actionsFor", { name: text.name(row.key) })}
          items={
            row.override
              ? [
                  {
                    label: t("planSection.editOverride"),
                    onSelect: () =>
                      setOpen({ dialog: "override", key: row.key }),
                  },
                  { separator: true },
                  {
                    label: t("planSection.removeOverride"),
                    tone: "destructive",
                    onSelect: () => setOpen({ dialog: "remove", row }),
                  },
                ]
              : [
                  {
                    label: t("planSection.addOverride"),
                    onSelect: () =>
                      setOpen({ dialog: "override", key: row.key }),
                  },
                ]
          }
        />
      );

  const body = (() => {
    if (plan.isPending)
      return <Skeleton rows={3} label={t("planSection.title")} />;
    if (plan.isError)
      return (
        <ListError
          title={t("planSection.error")}
          problem={problemOf(plan.error)}
          leave={leaveOf(plan.error)}
          at={plan.errorUpdatedAt}
          onRetry={() => void plan.refetch()}
        />
      );
    const rows = plan.data.entitlements;
    return (
      <>
        {!readOnly && rows.some((row) => overBy(row) > 0) && (
          <Banner
            tone="warning"
            title={t.rich("planSection.overTitle", { name, ...isolated })}
          >
            {t("planSection.overText")}
          </Banner>
        )}
        <Table
          columns={columns}
          rows={rows}
          rowKey="key"
          label={t("planSection.title")}
          actions={actions}
          actionsLabel={t("organisations.actions")}
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
        <p className="text-small text-muted-foreground">
          {rows.some((row) => row.override)
            ? t("planSection.hint")
            : t("planSection.noOverrides")}
        </p>
      </>
    );
  })();

  return (
    <section
      aria-labelledby={`plan-${organisation.id}`}
      className="flex min-w-0 flex-col gap-4"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col">
          <h2 id={`plan-${organisation.id}`} className="text-section-title">
            {t("planSection.title")}
          </h2>
          {plan.data && (
            <strong className="text-body">
              <bdi>{plan.data.plan.name}</bdi>
            </strong>
          )}
        </div>
        {!readOnly && plan.data && (
          <div className="flex flex-wrap gap-2">
            <Button onClick={() => setOpen({ dialog: "change" })}>
              {t("planSection.changePlan")}
            </Button>
            <Button
              icon={<Icon icon={PlusIcon} />}
              onClick={() => setOpen({ dialog: "override", key: null })}
            >
              {t("planSection.addOverride")}
            </Button>
          </div>
        )}
      </div>
      {organisation.status === TenantResponseStatus.Closed && (
        <Banner
          tone="info"
          title={t.rich("planSection.closedTitle", { name, ...isolated })}
        >
          {t("planSection.closedText")}
        </Banner>
      )}
      {body}
      {open?.dialog === "override" && plan.data && (
        <OverrideDialog
          organisation={organisation}
          plan={plan.data}
          entitlementKey={open.key}
          onClose={() => setOpen(null)}
        />
      )}
      {open?.dialog === "remove" && (
        <RemoveOverrideDialog
          organisation={organisation}
          row={open.row}
          onClose={() => setOpen(null)}
        />
      )}
      {open?.dialog === "change" && plan.data && (
        <ChangePlanDialog
          organisation={organisation}
          plan={plan.data}
          onClose={() => setOpen(null)}
        />
      )}
    </section>
  );
}
