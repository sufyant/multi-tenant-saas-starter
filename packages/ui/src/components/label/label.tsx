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
        "inline-flex h-5 shrink-0 items-center whitespace-nowrap rounded-sm px-1.5 align-middle text-small font-medium text-foreground inset-ring inset-ring-border",
        className,
      )}
    >
      {children}
    </span>
  );
}
