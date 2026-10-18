import type { ReactNode } from "react";
import { cn } from "../../lib/utils";

export type EmptyStateProps = {
  /** empty: nothing yet; no-results: a search matched nothing; error: loading failed. */
  kind?: "empty" | "no-results" | "error";
  title: ReactNode;
  description?: ReactNode;
  /** Error only: a reference and the UTC time for support, "Ref 7f3a9c · 13:32 UTC". */
  code?: string;
  action?: ReactNode;
};

/** What a list shows when it has nothing (the EmptyState README). No icon, no illustration, no apology. */
export function EmptyState({
  kind = "empty",
  title,
  description,
  code,
  action,
}: EmptyStateProps) {
  return (
    <div
      role={kind === "error" ? "alert" : undefined}
      className={cn(
        "flex flex-col items-center gap-2 rounded-lg border border-border px-6 py-12 text-center",
        kind === "error" ? "border-solid" : "border-dashed",
      )}
    >
      <h2 className="text-section-title">{title}</h2>
      {description && (
        <p className="max-w-prose text-body text-muted-foreground">
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
