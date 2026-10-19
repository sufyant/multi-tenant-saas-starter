import type { ReactNode } from "react";
import { cn } from "../../lib/utils";

export type StatusTone =
  | "success"
  | "warning"
  | "info"
  | "neutral"
  | "destructive";

const tones: Record<StatusTone, { dot: string; text: string }> = {
  success: { dot: "bg-success", text: "text-success-text" },
  warning: { dot: "bg-warning", text: "text-warning-text" },
  info: { dot: "bg-info", text: "text-info-text" },
  neutral: { dot: "bg-neutral", text: "text-neutral-text" },
  destructive: { dot: "bg-destructive", text: "text-destructive-text" },
};

export type StatusProps = {
  /** Status meaning: success normal, warning caution, info pending, neutral ended, destructive act now. */
  tone?: StatusTone;
  /** The word. Colour is never the only signal. */
  children: ReactNode;
  className?: string;
};

/** A 7px dot and a word (the Status README). Never a filled badge. */
export function Status({ tone = "neutral", children, className }: StatusProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 text-status",
        tones[tone].text,
        className,
      )}
    >
      <span
        aria-hidden="true"
        className={cn("size-[7px] shrink-0 rounded-full", tones[tone].dot)}
      />
      {children}
    </span>
  );
}
