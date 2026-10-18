import type { ReactNode } from "react";
import { cn } from "../../lib/utils";

export type TagProps = { children: ReactNode; className?: string };

/** A small neutral label after a name, such as "You" (the Tag README). It never carries a status. */
export function Tag({ children, className }: TagProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-sm border border-border px-1.5 text-small font-medium text-muted-foreground",
        className,
      )}
    >
      {children}
    </span>
  );
}
