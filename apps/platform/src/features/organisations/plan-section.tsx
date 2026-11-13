"use client";

import {
  EntitlementOverrideResponseState,
  getGetTenantPlanQueryOptions,
  LimitKey,
  problemOf,
  type TenantDetailResponse,
  type TenantEntitlementResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { ListError } from "@repo/ui/components/list-error";
import { Pill } from "@repo/ui/components/pill";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { type ReactNode, useId, useState } from "react";
import {
  entitlementAreas,
  retentionKey,
  useEntitlementText,
} from "@/shared/entitlements";
import { isolated } from "@/shared/isolated";
import { leaveOf } from "@/shared/problem-screen";
import { useDates } from "@/shared/use-dates";
import { ChangePlanDialog } from "./change-plan-dialog";
import { useMembersText } from "./members-text";
import { waitsInOrganisation } from "./organisation";
import { OverrideDialog } from "./override-dialog";
import { usePersonName } from "./person-name";
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
 * An organisation's Plan tab (4.14): a card of rows, the plan's name first, then a row per key with the value that applies (a limit
 * with its count, "12 of 15"), what the plan gives and the override (value, expiry, reason, who set it), with Edit and Remove; Change
 * plan and Add override under the card. The seats row says the members sentence of the Overview. An expired override stays listed,
 * crossed out, until someone edits or removes it. A closed or purging organisation's plan and overrides are read-only (409
 * `tenant.not_active`); a suspended one's can change. Every system admin holds every platform permission (the API's A4).
 */
export function PlanSection({
  organisation,
}: {
  organisation: TenantDetailResponse;
}) {
  const t = useTranslations();
  const text = useEntitlementText();
  const { date } = useDates();
  const membersText = useMembersText();
  const [open, setOpen] = useState<Open | null>(null);
  const plan = useQuery(getGetTenantPlanQueryOptions(organisation.id));
  const readOnly =
    organisation.status === TenantResponseStatus.Closed ||
    organisation.status === TenantResponseStatus.Purging;
  const name = organisation.name;
  const personName = usePersonName();
  const waitsId = useId();

  /**
   * The value that applies: a limit with a number says how much of it the organisation uses; the Activity log counts nothing. A
   * limit whose feature is off says Off (4.50).
   */
  const applies = (row: TenantEntitlementResponse, waits: boolean) =>
    waits
      ? text.off()
      : !row.isFeature &&
          row.key !== retentionKey &&
          !row.effective.unlimited &&
          row.effective.maximum !== null
        ? t("planSection.countOf", {
            count: row.count,
            limit: row.effective.maximum,
          })
        : text.label(row.isFeature, row.effective, row.key);

  /** What the plan gives, then the override: "Team gives 5. Override: 10 until 31 Dec 2026 · Reason: pilot · by Deniz Kaya". */
  const details = (row: TenantEntitlementResponse, planName: string) => {
    const override = row.override;
    const value =
      override && text.label(row.isFeature, override.value, row.key);
    return (
      <span className="text-small text-muted-foreground">
        {row.key === LimitKey.MembersMax
          ? membersText(organisation, plan.data).sentence
          : t.rich("planSection.planGives", {
              plan: () => <bdi>{planName}</bdi>,
              value: text.label(row.isFeature, row.planValue, row.key),
            })}
        {override && " "}
        {override &&
          t.rich(
            override.expiresAt
              ? "planSection.overrideUntil"
              : "planSection.overrideOpen",
            {
              value: () =>
                isExpired(row) ? (
                  <>
                    <del>{value}</del>{" "}
                    <Pill tone="neutral">{t("planSection.expired")}</Pill>
                  </>
                ) : (
                  value
                ),
              date: override.expiresAt ? date(override.expiresAt) : "",
              reason: () => <bdi>{override.reason}</bdi>,
              name: () => <bdi>{personName(override.setBy)}</bdi>,
            },
          )}
      </span>
    );
  };

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
    const { plan: current } = plan.data;
    // The keys in the frames' order of areas (4.14, 6.04): the seats, then the roles; a key the list does not know comes last.
    const order = entitlementAreas().flatMap(({ keys }) =>
      keys.map(({ key }) => key),
    );
    const place = (key: string) =>
      order.includes(key) ? order.indexOf(key) : order.length;
    const rows = [...plan.data.entitlements].sort(
      (one, other) => place(one.key) - place(other.key),
    );
    const waits = (row: TenantEntitlementResponse) =>
      waitsInOrganisation(plan.data, row);
    // A limit whose feature is off applies nothing yet, so it is over nothing (4.50).
    const over = (row: TenantEntitlementResponse) =>
      waits(row) ? 0 : overBy(row);
    return (
      <>
        {!readOnly && rows.some((row) => over(row) > 0) && (
          <Banner
            tone="warning"
            title={t.rich("planSection.overTitle", { name, ...isolated })}
          >
            {t("planSection.overText")}
          </Banner>
        )}
        <div className="flex flex-col divide-y divide-border rounded-lg border border-border bg-card">
          <Row label={t("planSection.title")}>
            <bdi>{current.name}</bdi>
          </Row>
          {rows.map((row) => (
            <Row
              key={row.key}
              label={text.name(row.key)}
              actions={
                !readOnly &&
                row.override && (
                  <>
                    <Button
                      aria-label={t("planSection.editOverrideOn", {
                        key: text.name(row.key),
                      })}
                      // An override of a limit whose feature is off cannot change; Remove stays allowed (4.50, 4.52).
                      disabled={waits(row)}
                      aria-describedby={
                        waits(row) ? `${waitsId}-${row.key}` : undefined
                      }
                      onClick={() =>
                        setOpen({ dialog: "override", key: row.key })
                      }
                    >
                      {t("planSection.edit")}
                    </Button>
                    <Button
                      variant="ghost"
                      aria-label={t("planSection.removeOverrideOn", {
                        key: text.name(row.key),
                      })}
                      onClick={() => setOpen({ dialog: "remove", row })}
                    >
                      {t("planSection.remove")}
                    </Button>
                  </>
                )
              }
            >
              <span className="flex flex-wrap items-center gap-2">
                <span>{applies(row, waits(row))}</span>
                {over(row) > 0 && (
                  <Pill tone="warning">
                    {t("planSection.overBy", { count: over(row) })}
                  </Pill>
                )}
              </span>
              {details(row, current.name)}
              {waits(row) && (
                <span
                  id={`${waitsId}-${row.key}`}
                  className="text-small text-muted-foreground"
                >
                  {text.requiresFeature(row.key)}
                </span>
              )}
            </Row>
          ))}
        </div>
        {!readOnly && (
          <div className="flex flex-wrap gap-2">
            <Button
              variant="primary"
              onClick={() => setOpen({ dialog: "change" })}
            >
              {t("planSection.changePlan")}
            </Button>
            <Button onClick={() => setOpen({ dialog: "override", key: null })}>
              {t("planSection.addOverride")}
            </Button>
          </div>
        )}
      </>
    );
  })();

  return (
    <section
      aria-label={t("planSection.title")}
      className="flex min-w-0 flex-col gap-4"
    >
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
      {open?.dialog === "remove" && plan.data && (
        <RemoveOverrideDialog
          organisation={organisation}
          plan={plan.data}
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

/** One row of the card (4.14): its name in a column, the value and its details after it, the row's buttons at the end. */
function Row({
  label,
  actions,
  children,
}: {
  label: string;
  actions?: ReactNode;
  children: ReactNode;
}) {
  const labelId = useId();
  return (
    // biome-ignore lint/a11y/useSemanticElements: a fieldset would draw a frame the card does not have; the group names the row.
    <div
      role="group"
      aria-labelledby={labelId}
      className="flex flex-col gap-2 px-5 py-3 tablet:flex-row tablet:items-center tablet:gap-6"
    >
      <span
        id={labelId}
        className="text-small text-muted-foreground tablet:w-48 tablet:shrink-0"
      >
        {label}
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-0.5 text-body">
        {children}
      </div>
      {actions && <div className="flex shrink-0 gap-2">{actions}</div>}
    </div>
  );
}
