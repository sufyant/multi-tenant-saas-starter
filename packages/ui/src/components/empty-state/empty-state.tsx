import type { ReactNode } from "react";
import { cn } from "../../lib/utils";
import { Icon, type LucideIcon } from "../icon";

export type EmptyStateProps = {
  /** empty: nothing yet; no-results: a search matched nothing; error: loading failed. */
  kind?: "empty" | "no-results" | "error";
  title: ReactNode;
  description?: ReactNode;
  /** Error only: a reference and the UTC time for support, "Ref 7f3a9c · 13:32 UTC". */
  code?: string;
  action?: ReactNode;
  /** The 20 px icon in its tile above the title, when the page has one (the design's Empty state). */
  icon?: LucideIcon;
  /**
   * list: in place of a list's rows; page: the whole content of a page, such as "Page not found" and "You can't open this page",
   * 120 px below the top bar from bp-tablet (2.15, 2.40) and 40 px on a phone (2.43); compact: inside a popover's list, a small
   * title under the icon without its tile, lower on a phone's full screen (the bell's list, 3.03, 3.05, 3.15).
   */
  size?: "list" | "page" | "compact";
};

/**
 * What a list or a page shows when it has nothing to show (the design's Empty state): what will appear and how, or what went
 * wrong, one action at most, no apology.
 */
export function EmptyState({
  kind = "empty",
  title,
  description,
  code,
  action,
  icon,
  size = "list",
}: EmptyStateProps) {
  const compact = size === "compact";
  return (
    <div
      role={kind === "error" ? "alert" : undefined}
      className={cn(
        "flex flex-col items-center text-center",
        compact ? "gap-1.5 px-4 py-7 max-tablet:pt-40" : "gap-2 px-6 py-10",
        size === "page" && "tablet:pt-30",
      )}
    >
      {icon &&
        (compact ? (
          <Icon icon={icon} size={20} className="max-tablet:size-4" />
        ) : (
          <span className="flex size-10 items-center justify-center rounded-lg bg-muted">
            <Icon icon={icon} size={20} />
          </span>
        ))}
      <h2 className={compact ? "text-small font-medium" : "text-section-title"}>
        {title}
      </h2>
      {description && (
        <p className="max-w-prose text-small text-muted-foreground">
          {description}
        </p>
      )}
      {kind === "error" && code && (
        <p dir="ltr" className="font-mono text-mono text-muted-foreground">
          {code}
        </p>
      )}
      {action && <div className={compact ? undefined : "mt-2"}>{action}</div>}
    </div>
  );
}
