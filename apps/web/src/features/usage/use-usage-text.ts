"use client";

import {
  MeterSettingResponseOverage,
  type UsageResponse,
} from "@repo/api-client";
import {
  formatDate,
  formatDayMonth,
  formatMonthYear,
} from "@repo/ui/lib/format-date-time";
import { formatMoney } from "@repo/ui/lib/format-money";
import { useTranslations } from "next-intl";

/**
 * The Usage screens' values as text: a meter's name, a count, an amount in the deployment's currency, and the days of the month, which
 * the API answers as calendar days of UTC, so they read in UTC (E8).
 */
export function useUsageText(usage: UsageResponse) {
  const t = useTranslations();
  const locale = t("locale.format");
  const days = { locale, timeZone: "UTC" };
  const number = new Intl.NumberFormat(locale, { numberingSystem: "latn" });
  const money = (minor: number) => formatMoney(minor, usage.currency, locale);
  const count = (value: number) => number.format(value);
  // The product's own word for a meter; the template ships none, so a meter reads as its key, as in the emails (the API's E10).
  const meterName = (key: string) => {
    const message = `usage.meters.${key}` as Parameters<typeof t>[0];
    return t.has(message) ? t(message) : key;
  };
  const nextMonth = new Date(usage.periodEnd);
  nextMonth.setUTCDate(nextMonth.getUTCDate() + 1);
  const planText = usage.meters
    .map((meter) =>
      meter.overage === MeterSettingResponseOverage.Stop
        ? t("usage.stopText", {
            plan: usage.planName,
            quota: count(meter.quota),
            meter: meterName(meter.key),
          })
        : t("usage.chargeText", {
            plan: usage.planName,
            quota: count(meter.quota),
            meter: meterName(meter.key),
            price: money(meter.unitPrice),
            size: count(meter.unitSize),
          }),
    )
    .join(" ");
  return {
    meterName,
    money,
    count,
    planText,
    /** The first day of the next month, when the counts start again: "1 Dec". */
    resetsOn: formatDayMonth(nextMonth, days),
    dayMonth: (day: string) => formatDayMonth(day, days),
    date: (day: string) => formatDate(day, days),
    month: (day: string) => formatMonthYear(day, days),
    /** A moment of the month, such as the cap's, in UTC as the month is: "24 Nov". */
    day: (at: string) => formatDayMonth(at, days),
  };
}

export type UsageText = ReturnType<typeof useUsageText>;
