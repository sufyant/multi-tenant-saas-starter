import type { ReactNode } from "react";
import { cn } from "../../lib/utils";

export type PillTone =
  | "success"
  | "warning"
  | "info"
  | "destructive"
  | "neutral";

const tones: Record<PillTone, string> = {
  success: "bg-success-subtle text-success-text",
  warning: "bg-warning-subtle text-warning-text",
  info: "bg-info-subtle text-info-text",
  destructive: "bg-destructive-subtle text-destructive-text",
  neutral: "bg-neutral/16 text-neutral-text",
};

export type PillProps = {
  /**
   * What the colour means (the Pill tones pattern): success active or current, warning needs attention or suspended, info
   * scheduled or new, destructive being deleted or failed, neutral ended, closed or replaced.
   */
  tone: PillTone;
  /** The word. The colour never stands alone. */
  children: ReactNode;
  className?: string;
};

/** A status as a filled pill (the Pill tones pattern). One line, never cut: its words stay short. A role or a plain fact is a Label. */
export function Pill({ tone, children, className }: PillProps) {
  return (
    <span
      data-tone={tone}
      className={cn(
        "inline-flex h-5 shrink-0 items-center whitespace-nowrap rounded-full px-2 align-middle text-small font-medium",
        tones[tone],
        className,
      )}
    >
      {children}
    </span>
  );
}
