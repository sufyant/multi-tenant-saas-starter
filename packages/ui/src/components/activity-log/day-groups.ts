import { calendarDay } from "../../lib/format-date-time";

/** What a log entry needs to be grouped: its id and its moment, in UTC. */
export type LogEntry = { id: string; occurredAt: string };

export type DayGroup<T extends LogEntry> = {
  /** The calendar day in the viewer's zone, as `2026-10-16`. */
  day: string;
  entries: T[];
};

/** The entries, newest first, grouped by their calendar day in the viewer's zone (null: the browser's). */
export function groupByDay<T extends LogEntry>(
  entries: T[],
  timeZone: string | null | undefined,
): DayGroup<T>[] {
  const groups: DayGroup<T>[] = [];
  for (const entry of entries) {
    const day = calendarDay(entry.occurredAt, { timeZone });
    const last = groups.at(-1);
    if (last?.day === day) last.entries.push(entry);
    else groups.push({ day, entries: [entry] });
  }
  return groups;
}
