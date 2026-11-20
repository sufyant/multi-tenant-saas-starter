import type { UsageResponse } from "@repo/api-client";
import {
  daysAfter,
  formatDate,
  formatDayMonth,
  formatMonthYear,
} from "./format-date-time";
import { formatMoney } from "./format-money";

/**
 * The values of a month's usage as text, for the web's Usage and the platform's Usage tab (F29): a count, an amount in the
 * deployment's currency, and the days of the month. The API answers them as calendar days of UTC, so they read in UTC (the API's E8).
 */
export function usageFormat(
  usage: Pick<UsageResponse, "periodEnd" | "currency">,
  locale: string,
) {
  const days = { locale, timeZone: "UTC" };
  const number = new Intl.NumberFormat(locale, { numberingSystem: "latn" });
  return {
    count: (value: number) => number.format(value),
    money: (minor: number) => formatMoney(minor, usage.currency, locale),
    /** The first day of the next month, when the counts start again: "1 Dec". */
    resetsOn: formatDayMonth(daysAfter(usage.periodEnd, 1), days),
    /** A day or a moment of the month: "24 Nov". */
    day: (at: string) => formatDayMonth(at, days),
    date: (day: string) => formatDate(day, days),
    month: (day: string) => formatMonthYear(day, days),
  };
}

export type PastMonth = {
  periodStart: string;
  /** What each meter used, by its key. */
  meters: Record<string, number>;
  extra: number;
  extraAmount: number;
};

/** The past months as rows, the latest first, and every meter's key that this month or a past one has. */
export function pastMonthsOf(usage: UsageResponse): {
  keys: string[];
  rows: PastMonth[];
} {
  const keys = [
    ...new Set([
      ...usage.meters.map((meter) => meter.key),
      ...usage.pastPeriods.flatMap((period) =>
        period.meters.map((meter) => meter.key),
      ),
    ]),
  ];
  const rows = usage.pastPeriods.map((period) => ({
    periodStart: period.periodStart,
    meters: Object.fromEntries(
      period.meters.map((meter) => [meter.key, meter.used]),
    ),
    extra: period.meters.reduce((sum, meter) => sum + meter.extra, 0),
    extraAmount: period.meters.reduce(
      (sum, meter) => sum + meter.extraAmount,
      0,
    ),
  }));
  return { keys, rows };
}
