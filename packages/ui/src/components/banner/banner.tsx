import type { ReactNode } from "react";
import { cn } from "../../lib/utils";

export type BannerTone = "warning" | "destructive" | "info" | "success";

const tones: Record<BannerTone, string> = {
  warning: "bg-warning-subtle border-warning",
  destructive: "bg-destructive-subtle border-destructive",
  info: "bg-info-subtle border-info",
  success: "bg-success-subtle border-success",
};

export type BannerProps = {
  /** The fact, in one bold sentence: "Atlas Technic is suspended." */
  title: ReactNode;
  tone?: BannerTone;
  /** At most one action, a secondary button at the end. */
  action?: ReactNode;
  /** The consequence, and the reason when there is one. */
  children?: ReactNode;
};

/** A persistent strip that states the condition of the whole page (the Banner README). It does not dismiss. */
export function Banner({
  title,
  tone = "info",
  action,
  children,
}: BannerProps) {
  return (
    <div
      role="status"
      className={cn(
        "flex items-start gap-3 rounded-lg border-s-4 px-4 py-3 text-body",
        tones[tone],
      )}
    >
      <p className="flex-1">
        <strong className="font-semibold">{title}</strong> {children}
      </p>
      {action}
    </div>
  );
}
