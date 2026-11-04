import type { ReactNode } from "react";
import { cn } from "../../lib/utils";

export type LabelProps = {
  /** A role or a plain fact: "Owner", "Support lead", "System admin", "This device". */
  children: ReactNode;
  className?: string;
};

/** The neutral outlined label (the Pill tones pattern): a role or a plain fact, with no colour. One line, never cut. */
export function Label({ children, className }: LabelProps) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center whitespace-nowrap rounded-md border border-border px-1.5 py-px align-middle text-status text-foreground",
        className,
      )}
    >
      {children}
    </span>
  );
}
