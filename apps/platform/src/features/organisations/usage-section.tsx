"use client";

import {
  MeterSettingResponseOverage,
  type MeterUsageResponse,
  problemOf,
  type UsageResponse,
} from "@repo/api-client";
import { EmptyValue } from "@repo/ui/components/empty-value";
import { ListError } from "@repo/ui/components/list-error";
import { Meter } from "@repo/ui/components/meter";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import {
  type PastMonth,
  pastMonthsOf,
  usageFormat,
} from "@repo/ui/lib/usage-text";
import type { UseQueryResult } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { leaveOf } from "@/shared/problem-screen";

/**
 * The Usage tab of an organisation (4.37, 4.42; the API's E8): each meter of its plan this month against its quota, its plan's terms
 * and its spending cap in one line, and the last months, from GetTenantUsage. The page reads the query, as the tab shows only while
 * the plan has a meter: the template ships none.
 */
export function UsageSection({
  usage,
}: {
  usage: UseQueryResult<UsageResponse>;
}) {
  const t = useTranslations("usage");
  if (usage.isPending) return <Skeleton rows={4} label={t("loading")} />;
  if (usage.isError)
    return (
      <ListError
        title={t("loadError")}
        problem={problemOf(usage.error)}
        leave={leaveOf(usage.error)}
        at={usage.errorUpdatedAt}
        onRetry={() => void usage.refetch()}
      />
    );
  return <UsageOf usage={usage.data} />;
}

/** The values as text: `usageFormat` of `packages/ui`, as the web's Usage reads them (F29), and a meter's word. */
function useUsageText(usage: UsageResponse) {
  const t = useTranslations("usage");
  const locale = useTranslations()("locale.format");
  return {
    ...usageFormat(usage, locale),
    // The product's own word for a meter; the template ships none, so a meter reads as its key, as in the web and the emails (E10).
    meterName: (key: string) => {
      const message = `meters.${key}` as Parameters<typeof t>[0];
      return t.has(message) ? t(message) : key;
    },
  };
}

type UsageText = ReturnType<typeof useUsageText>;

function UsageOf({ usage }: { usage: UsageResponse }) {
  const t = useTranslations("usage");
  const text = useUsageText(usage);
  const cap = usage.spendingCapOffered ? usage.spendingCap : null;
  const terms = usage.meters.map((meter) =>
    meter.overage === MeterSettingResponseOverage.Stop
      ? t("stopTerms", {
          plan: usage.planName,
          quota: text.count(meter.quota),
          meter: text.meterName(meter.key),
        })
      : t("chargeTerms", {
          plan: usage.planName,
          quota: text.count(meter.quota),
          meter: text.meterName(meter.key),
          price: text.money(meter.unitPrice),
          size: text.count(meter.unitSize),
        }),
  );
  if (usage.spendingCapOffered)
    terms.push(
      cap ? t("capOn", { amount: text.money(cap.amount) }) : t("capOff"),
    );
  return (
    <>
      <section className="flex flex-col rounded-lg border border-border bg-card">
        <div className="flex flex-col gap-0.5 border-b border-border px-5 py-4">
          <h2 className="text-section-title">
            {t("period", {
              start: text.day(usage.periodStart),
              end: text.date(usage.periodEnd),
            })}
          </h2>
          <p className="text-small text-muted-foreground">{terms.join(" ")}</p>
        </div>
        <div className="flex flex-col gap-6 px-5 py-5">
          {usage.meters.map((meter) => (
            <MeterRow
              key={meter.key}
              meter={meter}
              text={text}
              capReachedAt={cap?.reachedAt ?? null}
              paysByCard={usage.spendingCapOffered}
            />
          ))}
        </div>
      </section>
      <LastMonths usage={usage} text={text} />
    </>
  );
}

/** One meter this month (the Meter's states): its share, its extra and amount, or when it stopped, and when it resets. */
function MeterRow({
  meter,
  text,
  capReachedAt,
  paysByCard,
}: {
  meter: MeterUsageResponse;
  text: UsageText;
  capReachedAt: string | null;
  paysByCard: boolean;
}) {
  const t = useTranslations("usage");
  const stops = meter.overage === MeterSettingResponseOverage.Stop;
  const stoppedAt =
    stops && meter.used >= meter.quota
      ? meter.quotaReachedAt
      : !stops && capReachedAt
        ? capReachedAt
        : null;
  const stopped = stoppedAt !== null || (stops && meter.used >= meter.quota);
  const date = text.resetsOn;
  const aside =
    stopped && !stops
      ? t("stoppedAtCap", { amount: text.money(meter.extraAmount) })
      : stopped
        ? t("stopped")
        : meter.extra > 0
          ? t("extra", {
              extra: text.count(meter.extra),
              amount: text.money(meter.extraAmount),
            })
          : t("share", {
              share:
                meter.quota === 0
                  ? 100
                  : Math.floor((meter.used / meter.quota) * 100),
            });
  const meta = stoppedAt
    ? t("stoppedOn", { day: text.day(stoppedAt), date })
    : stopped
      ? t("stopReset", { date })
      : meter.extra > 0 && paysByCard
        ? t("resetsExtra", { date })
        : t("resets", { date });
  return (
    <Meter
      label={t("count", {
        used: text.count(meter.used),
        quota: text.count(meter.quota),
        meter: text.meterName(meter.key),
      })}
      aside={aside}
      meta={meta}
      used={meter.used}
      quota={meter.quota}
      stopped={stopped}
    />
  );
}

/** The last months, the latest first (4.37): what each meter used, and the month's extra and its amount. */
function LastMonths({
  usage,
  text,
}: {
  usage: UsageResponse;
  text: UsageText;
}) {
  const t = useTranslations();
  const { keys, rows } = pastMonthsOf(usage);
  // One meter names its extra in units too; several only add their amounts.
  const extraOf = (row: PastMonth) =>
    keys.length === 1
      ? t("usage.pastExtra", {
          extra: text.count(row.extra),
          amount: text.money(row.extraAmount),
        })
      : t("usage.pastAmount", { amount: text.money(row.extraAmount) });
  const columns: TableColumn<PastMonth>[] = [
    {
      key: "period",
      header: t("usage.month"),
      // 4.37's columns: the month 222 px with the card's padding, each meter 216 px with its gap, the extra fills.
      width: "w-55.5",
      render: (row) => text.month(row.periodStart),
      card: "title",
    },
    ...keys.map(
      (key): TableColumn<PastMonth> => ({
        key: `meter-${key}`,
        header: text.meterName(key),
        width: "w-54",
        render: (row) => text.count(row.meters[key] ?? 0),
        cardRender: (row) =>
          t("usage.pastUsed", {
            used: text.count(row.meters[key] ?? 0),
            meter: text.meterName(key),
          }),
      }),
    ),
    {
      key: "extra",
      header: t("usage.extraColumn"),
      render: (row) => (row.extraAmount > 0 ? extraOf(row) : <EmptyValue />),
      cardRender: (row) =>
        row.extraAmount > 0 ? extraOf(row) : t("usage.noExtra"),
    },
  ];
  return (
    <section className="flex flex-col rounded-lg border border-border bg-card">
      <div className="flex flex-col gap-0.5 px-5 py-4">
        <h2 className="text-section-title">{t("usage.lastMonths")}</h2>
        {rows.length === 0 && (
          <p className="text-small text-muted-foreground">
            {t("usage.noPast")}
          </p>
        )}
      </div>
      {rows.length > 0 && (
        <Table
          columns={columns}
          rows={rows}
          rowKey="periodStart"
          label={t("usage.lastMonths")}
          inCard
          rowCount={rows.length}
          page={1}
          pageSize={Math.max(1, rows.length)}
          pager={{
            previous: t("common.previous"),
            next: t("common.next"),
            page: (current, count) =>
              t("common.page", { page: current, count }),
          }}
        />
      )}
    </section>
  );
}
