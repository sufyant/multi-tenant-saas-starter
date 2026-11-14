import type { ReactNode } from "react";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import {
  CircleCheckIcon,
  CircleXIcon,
  Icon,
  InfoIcon,
  type LucideIcon,
  TriangleAlertIcon,
  XIcon,
} from "../icon";

export type BannerTone = "warning" | "destructive" | "info" | "success";

const tones: Record<BannerTone, { box: string; icon: LucideIcon }> = {
  warning: {
    box: "bg-warning-subtle text-warning-text",
    icon: TriangleAlertIcon,
  },
  destructive: {
    box: "bg-destructive-subtle text-destructive-text",
    icon: CircleXIcon,
  },
  info: { box: "bg-info-subtle text-info-text", icon: InfoIcon },
  success: {
    box: "bg-success-subtle text-success-text",
    icon: CircleCheckIcon,
  },
};

export type BannerProps = {
  /** The fact, in one bold sentence: "Atlas Logistics is suspended." */
  title: ReactNode;
  tone?: BannerTone;
  /** At most one action, a secondary button at the end. */
  action?: ReactNode;
  /** The consequence, and the reason when there is one. */
  children?: ReactNode;
  /** A dismissible banner (the Banner's "Dismissible"): a ghost × at the very end, named by `closeLabel`. */
  onClose?: () => void;
  /** The close button's accessible name, such as "Close". */
  closeLabel?: string;
};

/** A persistent strip that states the condition of the whole page (the design's Banner): the tone's icon, the title over the text. */
export function Banner({
  title,
  tone = "info",
  action,
  children,
  onClose,
  closeLabel,
}: BannerProps) {
  return (
    <div
      role="status"
      className={cn(
        "flex items-center gap-2.5 rounded-lg py-2.5 ps-3.5 pe-2 text-small",
        tones[tone].box,
      )}
    >
      <Icon icon={tones[tone].icon} size={16} className="shrink-0" />
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <p className="font-medium">{title}</p>
        {/* A flex column draws no space between its items; the space keeps the sentences apart in the text. */}
        {children && " "}
        {children && <p>{children}</p>}
      </div>
      {action}
      {onClose && (
        <Button
          variant="ghost"
          icon={<Icon icon={XIcon} size={16} />}
          aria-label={closeLabel}
          onClick={onClose}
          className="shrink-0 text-foreground"
        />
      )}
    </div>
  );
}
