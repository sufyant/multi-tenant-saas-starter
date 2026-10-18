"use client";

import {
  type DateTimeOptions,
  formatDate,
  formatDateTime,
  formatRelative,
  formatTime,
  formatUtc,
  type Instant,
  zoneLabel,
} from "../../lib/format-date-time";
import { cn } from "../../lib/utils";
import { Tooltip, TooltipContent, TooltipTrigger } from "../ui/tooltip";

export type TimeProps = DateTimeOptions & {
  /** An instant from the API, in UTC. */
  at: Instant;
  /** datetime (default), date, time, or relative ("12 minutes ago"). */
  show?: "datetime" | "date" | "time" | "relative";
  /** The reference "now" of a relative time. Defaults to the current time. */
  now?: Instant;
  /** Previews only: render the tooltip open. */
  open?: boolean;
  className?: string;
};

const shown = {
  datetime: formatDateTime,
  date: formatDate,
  time: formatTime,
  relative: (at: Instant, options: DateTimeOptions & { now?: Instant }) =>
    formatRelative(at, options),
};

/**
 * Every date and time on screen (the Time README): local time only, and on hover or focus, after 300ms, the full local date and
 * time, the zone with its UTC offset and the same moment in UTC. Only `format-date-time.ts` formats (F18).
 */
export function Time({
  at,
  locale,
  timeZone,
  show = "datetime",
  now,
  open,
  className,
}: TimeProps) {
  const options = { locale, timeZone };
  const iso = (typeof at === "string" ? new Date(at) : at).toISOString();
  return (
    <Tooltip defaultOpen={open}>
      <TooltipTrigger
        delay={300}
        className={cn(
          "cursor-default tabular-nums underline decoration-dotted decoration-muted-foreground underline-offset-4",
          className,
        )}
      >
        <time dateTime={iso}>{shown[show](at, { ...options, now })}</time>
      </TooltipTrigger>
      <TooltipContent className="flex-col items-start gap-0.5">
        <span>{formatDateTime(at, options)}</span>
        <span>{zoneLabel(at, options)}</span>
        <span dir="ltr" className="font-mono">
          {formatUtc(at, options)}
        </span>
      </TooltipContent>
    </Tooltip>
  );
}
