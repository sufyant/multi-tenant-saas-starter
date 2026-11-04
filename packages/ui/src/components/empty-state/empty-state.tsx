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
   * 120 px below the top bar from bp-tablet (2.15, 2.40) and 40 px on a phone (2.43).
   */
  size?: "list" | "page";
};

/** What a list shows when it has nothing (the design's Empty state): what will appear and how, one action at most, no apology. */
export function EmptyState({
  kind = "empty",
  title,
  description,
  code,
  action,
  icon,
  size = "list",
}: EmptyStateProps) {
  return (
    <div
      role={kind === "error" ? "alert" : undefined}
      className={cn(
        "flex flex-col items-center gap-2 px-6 py-10 text-center",
        size === "page" && "tablet:pt-30",
      )}
    >
      {icon && (
        <span className="flex size-10 items-center justify-center rounded-lg bg-muted">
          <Icon icon={icon} size={20} />
        </span>
      )}
      <h2 className="text-section-title">{title}</h2>
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
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}
