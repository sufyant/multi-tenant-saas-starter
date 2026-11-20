"use client";

import { calendarDay, formatDayMonth } from "@repo/ui/lib/format-date-time";
import { useTranslations } from "next-intl";
import { useDates } from "@/shared/use-dates";

/** The most days of a trial at once, as the API takes them (`trial.days_invalid`, `trial.end_invalid`). */
const mostDays = 365;

/** The whole number of days typed, 1 to 365; none for any other text. */
export const trialDays = (typed: string) => {
  if (!/^\d+$/.test(typed.trim())) return null;
  const days = Number(typed);
  return days >= 1 && days <= mostDays ? days : null;
};

/** A trial's dates: "22 Oct to 5 Nov 2026", with the first year only when the two years differ. */
export function useTrialDates() {
  const t = useTranslations();
  const dates = useDates();
  return (from: string, to: string) => {
    const options = { locale: t("locale.format"), timeZone: dates.timeZone };
    // The years in the viewer's time zone, as the dates are written (F18).
    const year = (at: string) =>
      calendarDay(at, { timeZone: dates.timeZone }).slice(0, 4);
    const sameYear = year(from) === year(to);
    return t("billingCard.trialRange", {
      from: sameYear ? formatDayMonth(from, options) : dates.date(from),
      to: dates.date(to),
    });
  };
}
