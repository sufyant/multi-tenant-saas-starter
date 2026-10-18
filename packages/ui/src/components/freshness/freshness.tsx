import type { DateTimeOptions, Instant } from "../../lib/format-date-time";
import { cn } from "../../lib/utils";
import { Time } from "../time";

export type FreshnessProps = DateTimeOptions & {
  /** When the data was loaded. */
  at: Instant;
  /** The translated word for "Updated". */
  label: string;
  /** The data is older than the product's limit: the dot turns amber. */
  stale?: boolean;
  /** Previews only: render the tooltip open. */
  open?: boolean;
  className?: string;
};

/** The line under a list title that answers "can I trust this?" (the Freshness README): a dot, "Updated" and the local time. */
export function Freshness({
  at,
  locale,
  timeZone,
  label,
  stale = false,
  open,
  className,
}: FreshnessProps) {
  return (
    <p
      className={cn(
        "inline-flex items-center gap-1 text-small text-muted-foreground",
        className,
      )}
    >
      <span
        aria-hidden="true"
        className={cn(
          "size-[7px] rounded-full",
          stale ? "bg-warning" : "bg-success",
        )}
      />
      {label}{" "}
      <Time
        at={at}
        locale={locale}
        timeZone={timeZone}
        show="time"
        open={open}
      />
    </p>
  );
}
