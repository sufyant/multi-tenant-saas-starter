import { cn } from "../../lib/utils";
import type { LucideIcon } from "./icons";

export type IconProps = {
  /** A Lucide icon from `./icons`. */
  icon: LucideIcon;
  /** 16 in rows, 18 in the rail. */
  size?: number;
  /** An arrow, a chevron, send or sign-out: it mirrors in right-to-left. A clock, a check or a logo does not. */
  directional?: boolean;
  /** The accessible name of an icon that stands alone. Without it, the icon is decoration and hidden from assistive technology. */
  label?: string;
  className?: string;
};

/** A Lucide icon at stroke 1.75 in the text colour (the Icon README). The only way the apps draw an icon (F22). */
export function Icon({
  icon: Glyph,
  size = 16,
  directional = false,
  label,
  className,
}: IconProps) {
  return (
    <Glyph
      size={size}
      strokeWidth={1.75}
      aria-hidden={label ? undefined : true}
      aria-label={label}
      role={label ? "img" : undefined}
      className={cn("shrink-0", directional && "rtl:-scale-x-100", className)}
    />
  );
}
