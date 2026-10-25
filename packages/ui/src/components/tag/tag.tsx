import type { ReactNode } from "react";
import { cn } from "../../lib/utils";
import { Icon, type LucideIcon } from "../icon";

export type TagProps = {
  children: ReactNode;
  /** badge (default) for a kind of record: "System admin", "Built in". text for the viewer's own row: "(you)". */
  variant?: "badge" | "text";
  /** Drawn before the text, such as the key of Root. */
  icon?: LucideIcon;
  className?: string;
};

/**
 * A label after a name (the Tag README): a kind of record is a quiet, borderless badge in the neutral tone; the viewer's own row
 * is muted text without a box. It never carries a status.
 */
export function Tag({
  children,
  variant = "badge",
  icon,
  className,
}: TagProps) {
  if (variant === "text")
    return (
      <span
        className={cn(
          "whitespace-nowrap font-normal text-muted-foreground",
          className,
        )}
      >
        {children}
      </span>
    );
  return (
    <span
      className={cn(
        "inline-flex h-5 items-center gap-1 whitespace-nowrap rounded-full bg-neutral/16 px-2 align-middle text-small font-medium text-neutral-text",
        className,
      )}
    >
      {icon && <Icon icon={icon} size={12} className="-ms-0.5" />}
      {children}
    </span>
  );
}
