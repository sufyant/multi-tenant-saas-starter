"use client";

import type { ReactNode } from "react";
import { calendarDay, dayBefore, formatDate } from "../../lib/format-date-time";
import { Button } from "../button";
import { Time } from "../time";
import { groupByDay, type LogEntry } from "./day-groups";

export type ActivityLogProps<T extends LogEntry> = {
  /** The entries of every page read so far, newest first, as a cursor list answers them (the API's K5). */
  entries: T[];
  /** One entry as a sentence. Each app writes its own, because the tenant's log and the platform's log differ. */
  sentence: (entry: T) => ReactNode;
  locale: string;
  /** The viewer's IANA zone: the days change at its midnight. Null or absent: the browser's. */
  timeZone?: string | null;
  labels: {
    today: (date: string) => string;
    yesterday: (date: string) => string;
    older: string;
  };
  /** The last page had a next cursor. */
  hasOlder: boolean;
  loadingOlder: boolean;
  onOlder: () => void;
};

/**
 * A log grouped by day in the viewer's zone (the Activity boards of both apps): a heading for each day, today and yesterday by
 * name, each entry with its local time, and a button that reads the next page while there is one.
 */
export function ActivityLog<T extends LogEntry>({
  entries,
  sentence,
  locale,
  timeZone,
  labels,
  hasOlder,
  loadingOlder,
  onOlder,
}: ActivityLogProps<T>) {
  const today = calendarDay(new Date(), { timeZone });
  return (
    <>
      {groupByDay(entries, timeZone).map((group) => {
        const date = formatDate(group.entries[0]?.occurredAt ?? group.day, {
          locale,
          timeZone,
        });
        const label =
          group.day === today
            ? labels.today(date)
            : group.day === dayBefore(today)
              ? labels.yesterday(date)
              : date;
        return (
          <section key={group.day} className="flex flex-col gap-1.5">
            <h2 className="mt-2 text-small text-muted-foreground">{label}</h2>
            <ul className="rounded-lg border border-border bg-card">
              {group.entries.map((entry) => (
                <li
                  key={entry.id}
                  className="grid grid-cols-[4rem_minmax(0,1fr)] gap-4 border-t border-border px-4 py-3 text-body first:border-t-0"
                >
                  <span className="text-muted-foreground">
                    <Time
                      at={entry.occurredAt}
                      show="time"
                      locale={locale}
                      timeZone={timeZone}
                    />
                  </span>
                  {sentence(entry)}
                </li>
              ))}
            </ul>
          </section>
        );
      })}
      {hasOlder && (
        <div className="flex justify-center py-2">
          <Button onClick={onOlder} disabled={loadingOlder}>
            {labels.older}
          </Button>
        </div>
      )}
    </>
  );
}
