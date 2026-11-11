import { cn } from "../../lib/utils";

export type OrgTileProps = {
  /** The organisation's name; the tile shows its first letter. */
  name: string;
  /** sm: a menu's row (2.03); md: the sidebar's switcher (2.01); lg: a list of organisations (1.18). */
  size?: "sm" | "md" | "lg";
  className?: string;
};

const sizes = {
  sm: "size-6 rounded-md text-status",
  md: "size-6 rounded-md text-small",
  lg: "size-8 rounded-lg text-body",
};

/**
 * The organisation's tile: its first letter on the info tint, in every place. Only the product's mark keeps the brand's gradient
 * (the maintainer's decision of 2026-11-08). The name beside it is the accessible text, so the tile is hidden from assistive technology.
 */
export function OrgTile({ name, size = "md", className }: OrgTileProps) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        "flex shrink-0 items-center justify-center bg-info-subtle font-medium text-info-text",
        sizes[size],
        className,
      )}
    >
      {name.charAt(0)}
    </span>
  );
}
