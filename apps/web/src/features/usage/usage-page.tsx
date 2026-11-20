"use client";

import {
  getGetPlanChoicesQueryOptions,
  MeterSettingResponseOverage,
  type MeterUsageResponse,
  Permission,
  type PlanChoiceResponse,
  problemOf,
  type UsageResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { EmptyValue } from "@repo/ui/components/empty-value";
import { ListError } from "@repo/ui/components/list-error";
import { Meter } from "@repo/ui/components/meter";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { type PastMonth, pastMonthsOf } from "@repo/ui/lib/usage-text";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { notFound } from "next/navigation";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { isBillingOff, useBilling, usePlanChange } from "@/shared/billing";
import { PermissionDenied } from "@/shared/permission-denied";
import { leaveOf } from "@/shared/problem-screen";
import { useCan, useTenant } from "@/shared/tenant-context";
import { useUsage } from "@/shared/usage";
import { type UsageText, useUsageText } from "@/shared/use-usage-text";
import { SpendingCapDialog } from "./spending-cap-dialog";

/**
 * Settings > Usage (11.01 to 11.11, 14.12, 14.15): each meter of the plan this month against its quota, the spending cap of an organisation
 * that pays by card (E9), and the past months. For a viewer who manages billing, while the deployment's billing is on and the plan has
 * a meter (F66).
 */
export function UsagePage() {
  const t = useTranslations();
  const can = useCan();
  const manages = can(Permission.TenantBillingManage);
  const billing = useBilling();
  const usage = useUsage();

  if (!manages)
    return (
      <Frame>
        <PermissionDenied page={t("usage.title")} />
      </Frame>
    );
  if (billing.isError && isBillingOff(billing.error)) notFound();
  const header = <Header usage={usage.data ?? null} />;
  if (usage.error)
    return (
      <Frame>
        {header}
        <ListError
          title={t("usage.loadError")}
          problem={problemOf(usage.error)}
          leave={leaveOf(usage.error)}
          at={usage.errorUpdatedAt}
          onRetry={() => void usage.refetch()}
        />
      </Frame>
    );
  if (usage.data?.meters.length === 0) notFound();
  if (!usage.data)
    return (
      <Frame>
        {header}
        <Skeleton rows={5} label={t("usage.loading")} />
      </Frame>
    );
  return (
    <Frame>
      {header}
      <UsageContent usage={usage.data} />
    </Frame>
  );
}

function UsageContent({ usage }: { usage: UsageResponse }) {
  const t = useTranslations();
  const text = useUsageText(usage);
  const [capOpen, setCapOpen] = useState(false);
  const cap = usage.spendingCapOffered ? usage.spendingCap : null;
  const capReached = cap?.reachedAt != null;
  const charged = usage.meters.filter(
    (meter) => meter.overage === MeterSettingResponseOverage.Charge,
  );
  const usedUp = usage.meters.find(
    (meter) =>
      meter.overage === MeterSettingResponseOverage.Stop &&
      meter.used >= meter.quota,
  );
  const capButton = (
    <Button variant="secondary" onClick={() => setCapOpen(true)}>
      {t(cap ? "usage.changeCap" : "usage.setCap")}
    </Button>
  );
  return (
    <>
      {cap && capReached && (
        <Banner
          tone="destructive"
          title={t("usage.capReachedTitle", {
            meter:
              charged.length === 1 && charged[0]
                ? text.meterName(charged[0].key)
                : t("usage.chargedMeters"),
            amount: text.money(cap.amount),
          })}
          action={capButton}
        >
          {t("usage.capReachedText", { date: text.resetsOn })}
        </Banner>
      )}
      {usedUp && <UsedUpBanner meter={usedUp} text={text} />}
      <Card title={t("usage.thisPeriod")} text={text.planText}>
        <div className="flex flex-col gap-6 px-5 pb-5">
          {usage.meters.map((meter) => (
            <MeterRow
              key={meter.key}
              meter={meter}
              text={text}
              capReached={capReached}
              paysByCard={usage.spendingCapOffered}
            />
          ))}
        </div>
      </Card>
      {usage.spendingCapOffered && (
        <Card
          title={t("usage.capTitle")}
          text={t("usage.capText")}
          action={capButton}
        >
          <dl className="flex flex-col gap-1 border-t border-border px-5 py-3 tablet:flex-row tablet:gap-4">
            <dt className="text-body text-muted-foreground tablet:w-50 tablet:shrink-0">
              {t("usage.cap")}
            </dt>
            <dd className="text-body">
              {!cap
                ? t("usage.capOff")
                : capReached && cap.reachedAt
                  ? t("usage.capReached", {
                      amount: text.money(cap.amount),
                      date: text.day(cap.reachedAt),
                    })
                  : t("usage.capOn", {
                      amount: text.money(cap.amount),
                      used: text.money(cap.extraAmount),
                    })}
            </dd>
          </dl>
        </Card>
      )}
      <PastPeriods usage={usage} text={text} />
      {capOpen && (
        <SpendingCapDialog
          amount={cap?.amount ?? null}
          currency={usage.currency}
          onClose={() => setCapOpen(false)}
        />
      )}
    </>
  );
}

/** One meter this month (the Meter's states): its share, its extra and amount, or why it stopped, and when it resets. */
function MeterRow({
  meter,
  text,
  capReached,
  paysByCard,
}: {
  meter: MeterUsageResponse;
  text: UsageText;
  capReached: boolean;
  paysByCard: boolean;
}) {
  const t = useTranslations();
  const name = text.meterName(meter.key);
  const stops = meter.overage === MeterSettingResponseOverage.Stop;
  const stoppedByQuota = stops && meter.used >= meter.quota;
  const stoppedByCap = !stops && capReached;
  const date = text.resetsOn;
  const aside = stoppedByQuota
    ? t("usage.stopped")
    : stoppedByCap
      ? t("usage.stoppedAtCap", { amount: text.money(meter.extraAmount) })
      : meter.extra > 0
        ? t("usage.extra", {
            extra: text.count(meter.extra),
            amount: text.money(meter.extraAmount),
          })
        : t("usage.share", {
            share:
              meter.quota === 0
                ? 100
                : Math.floor((meter.used / meter.quota) * 100),
          });
  const meta = stoppedByQuota
    ? t("usage.stopReset", { date })
    : stoppedByCap
      ? t("usage.capReset", { meter: name, date })
      : meter.extra > 0 && paysByCard
        ? t("usage.resetsExtra", { date })
        : meter.used === 0
          ? t("usage.resetsNothing", { date })
          : t("usage.resets", { date });
  return (
    <Meter
      label={t("usage.count", {
        used: text.count(meter.used),
        quota: text.count(meter.quota),
        meter: name,
      })}
      aside={aside}
      meta={meta}
      used={meter.used}
      quota={meter.quota}
      stopped={stoppedByQuota || stoppedByCap}
    />
  );
}

/**
 * A stop meter used its whole quota (11.05): it resets on the first of the next month; a viewer who can change the plan gets the
 * cheapest plan that gives more of it.
 */
function UsedUpBanner({
  meter,
  text,
}: {
  meter: MeterUsageResponse;
  text: UsageText;
}) {
  const t = useTranslations();
  const { tenant, detail } = useTenant();
  const change = usePlanChange() === "change";
  const choices = useQuery({
    ...getGetPlanChoicesQueryOptions(tenant.id),
    enabled: change,
  });
  const more = change ? morePlanOf(choices.data?.plans ?? [], meter) : null;
  return (
    <Banner
      tone="warning"
      title={t("usage.usedUpTitle", {
        quota: text.count(meter.quota),
        meter: text.meterName(meter.key),
        date: text.resetsOn,
      })}
      action={
        more && (
          <Button
            variant="secondary"
            render={<Link href={`/${detail.slug}/settings/billing/plans`} />}
            nativeButton={false}
          >
            {t("usage.changePlan")}
          </Button>
        )
      }
    >
      {more &&
        t("usage.usedUpText", {
          plan: more.plan.name,
          quota: text.count(more.quota),
        })}
    </Banner>
  );
}

/** The cheapest plan, by its monthly base fee and fee per person, whose quota of the meter is larger than this one's. */
function morePlanOf(plans: PlanChoiceResponse[], meter: MeterUsageResponse) {
  const priced = (plan: PlanChoiceResponse) =>
    plan.prices.baseMonthly + plan.prices.perPersonMonthly;
  return (
    plans
      .flatMap((plan) => {
        const setting = plan.meters.find((item) => item.key === meter.key);
        return setting && setting.quota > meter.quota
          ? [{ plan, quota: setting.quota }]
          : [];
      })
      .sort((a, b) => priced(a.plan) - priced(b.plan))[0] ?? null
  );
}

/** The past months, the latest first (11.01): what each meter used, and the month's extra and its amount. */
function PastPeriods({
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
      header: t("usage.period"),
      render: (row) => text.month(row.periodStart),
      card: "title",
    },
    ...keys.map(
      (key): TableColumn<PastMonth> => ({
        key: `meter-${key}`,
        header: text.meterName(key),
        align: "end",
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
    <Card
      title={t("usage.past")}
      text={rows.length === 0 ? t("usage.noPast") : null}
    >
      {rows.length > 0 && (
        <Table
          columns={columns}
          rows={rows}
          rowKey="periodStart"
          label={t("usage.past")}
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
    </Card>
  );
}

/** The title, and the month's days once GetUsage answered: "1 Nov to 30 Nov 2026". */
function Header({ usage }: { usage: UsageResponse | null }) {
  const t = useTranslations();
  return (
    <div className="flex flex-col gap-0.5">
      <h1 className="text-page-title">{t("usage.title")}</h1>
      {usage && <Period usage={usage} />}
    </div>
  );
}

function Period({ usage }: { usage: UsageResponse }) {
  const t = useTranslations();
  const { detail } = useTenant();
  const text = useUsageText(usage);
  return (
    <p className="text-body text-muted-foreground">
      {t("usage.description", {
        organisation: detail.name,
        start: text.day(usage.periodStart),
        end: text.date(usage.periodEnd),
      })}
    </p>
  );
}

function Card({
  title,
  text,
  action,
  children,
}: {
  title: string;
  text: ReactNode;
  action?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <section className="overflow-clip rounded-lg border border-border bg-card">
      <div className="flex items-start justify-between gap-4 p-5">
        <div className="flex flex-col gap-0.5">
          <h2 className="text-section-title">{title}</h2>
          {text && <p className="text-small text-muted-foreground">{text}</p>}
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}

function Frame({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-4">{children}</div>;
}
