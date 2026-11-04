import type { ReactNode } from "react";
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
};

/** What a list shows when it has nothing (the design's Empty state): what will appear and how, one action at most, no apology. */
export function EmptyState({
  kind = "empty",
  title,
  description,
  code,
  action,
  icon,
}: EmptyStateProps) {
  return (
    <div
      role={kind === "error" ? "alert" : undefined}
      className="flex flex-col items-center gap-2 px-6 py-10 text-center"
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
