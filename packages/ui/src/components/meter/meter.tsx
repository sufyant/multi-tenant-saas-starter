import type { ReactNode } from "react";
import { cn } from "../../lib/utils";

export type MeterProps = {
  /** The count against the quota, the meter's accessible name: "7,200 of 10,000 AI requests". */
  label: string;
  /** The text at the end of the top line: the share used, the extra and its amount, or why it stopped. */
  aside?: ReactNode;
  /** The line under the bar: "Resets on 1 Dec". */
  meta?: ReactNode;
  used: number;
  quota: number;
  /** The meter stopped: the quota of a stop meter is used, or the spending cap is reached. */
  stopped?: boolean;
};

/** Near starts at 80% of the quota (the Meter component's description). */
const near = 0.8;

/**
 * One usage meter (the design's Meter, 229:1163): the count over its quota, a bar, and a line under it. Normal fills the part used
 * in primary; Near, from 80%, in warning; Over, past the quota, the quota in primary and the extra after it in warning; Stopped fills
 * the whole track in destructive.
 */
export function Meter({
  label,
  aside,
  meta,
  used,
  quota,
  stopped = false,
}: MeterProps) {
  const over = !stopped && used > quota;
  const share = quota === 0 ? 1 : used / quota;
  const width = stopped ? 1 : over ? quota / used : Math.min(share, 1);
  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-baseline justify-between gap-4">
        <span className="text-body font-medium">{label}</span>
        {aside && (
          <span
            className={cn(
              "text-small",
              stopped
                ? "text-destructive-text"
                : over
                  ? "text-warning-text"
                  : "text-muted-foreground",
            )}
          >
            {aside}
          </span>
        )}
      </div>
      {/* biome-ignore lint/a11y/useSemanticElements: a native meter draws one bar; the Over state draws the quota and the extra apart. */}
      <div
        role="meter"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={Math.max(quota, used)}
        aria-valuenow={used}
        className="flex h-1.5 overflow-hidden rounded-full bg-track"
      >
        <div
          data-testid="meter-bar"
          className={cn(
            "h-full rounded-full",
            stopped
              ? "bg-destructive"
              : !over && share >= near
                ? "bg-warning"
                : "bg-primary",
          )}
          style={{ width: `${width * 100}%` }}
        />
        {over && (
          <div data-testid="meter-extra" className="h-full flex-1 bg-warning" />
        )}
      </div>
      {meta && <p className="text-small text-muted-foreground">{meta}</p>}
    </div>
  );
}
