import type { AuditEntryResponse } from "@repo/api-client";
import { calendarDay } from "@repo/ui/lib/format-date-time";

export type DayGroup = {
  /** The calendar day in the viewer's zone, as `2026-10-16`. */
  day: string;
  entries: AuditEntryResponse[];
};

/** The entries, newest first, grouped by their calendar day in the viewer's zone (null: the browser's). */
export function groupByDay(
  entries: AuditEntryResponse[],
  timeZone: string | null | undefined,
): DayGroup[] {
  const groups: DayGroup[] = [];
  for (const entry of entries) {
    const day = calendarDay(entry.occurredAt, { timeZone });
    const last = groups.at(-1);
    if (last?.day === day) last.entries.push(entry);
    else groups.push({ day, entries: [entry] });
  }
  return groups;
}

/** The calendar day before a day: `2026-03-01` gives `2026-02-28`. */
export function dayBefore(day: string): string {
  const [year, month, date] = day.split("-").map(Number);
  return new Date(Date.UTC(year ?? 0, (month ?? 1) - 1, (date ?? 1) - 1))
    .toISOString()
    .slice(0, 10);
}
