"use client";

import { Radio } from "@base-ui/react/radio";
import { RadioGroup } from "@base-ui/react/radio-group";
import { cva } from "class-variance-authority";
import { cn } from "../../lib/utils";
import { Icon } from "../icon";
import type { LucideIcon } from "../icon/icons";

export type SegmentedOption<T extends string> = {
  value: T;
  label: string;
  icon?: LucideIcon;
};

const track = cva(
  "inline-flex items-stretch gap-1 rounded-md border border-border bg-background p-1 data-disabled:opacity-45",
  {
    variants: {
      size: {
        default: "h-control-md",
        touch: "h-control-lg",
        // The touch size on a phone and a tablet, the default size from bp-desktop (the design's Profile frames).
        adaptive: "h-control-lg desktop:h-control-md",
      },
    },
    defaultVariants: { size: "default" },
  },
);

/**
 * The design system's segmented control: 2 or 3 options, all visible, of which exactly one holds. It is a radio group (WAI-ARIA
 * "Radio Group"): Tab reaches the chosen option, the arrow keys move the choice. The chosen option is the card with a 1 px input
 * border, so it stands out from the track at 3:1 (WCAG 2.2, 1.4.11). Right to left reverses the order by the flex direction.
 */
export function SegmentedControl<T extends string>({
  label,
  value,
  options,
  onValueChange,
  size,
  disabled,
  className,
}: {
  /** The accessible name of the group; the screen shows its own label above it. */
  label: string;
  value: T;
  options: SegmentedOption<T>[];
  onValueChange: (value: T) => void;
  size?: "default" | "touch" | "adaptive";
  disabled?: boolean;
  className?: string;
}) {
  return (
    <RadioGroup
      aria-label={label}
      value={value}
      onValueChange={(next) => onValueChange(next as T)}
      disabled={disabled}
      className={cn(track({ size }), className)}
    >
      {options.map((option) => (
        <Radio.Root
          key={option.value}
          value={option.value}
          nativeButton
          render={<button type="button" />}
          className="inline-flex flex-1 items-center justify-center gap-1 rounded-sm border border-transparent px-2 text-small whitespace-nowrap text-muted-foreground outline-none hover:not-data-checked:not-data-disabled:bg-accent focus-visible:ring-2 focus-visible:ring-ring data-checked:border-input data-checked:bg-card data-checked:font-medium data-checked:text-foreground data-checked:shadow-xs disabled:cursor-not-allowed"
        >
          {option.icon && <Icon icon={option.icon} />}
          {option.label}
        </Radio.Root>
      ))}
    </RadioGroup>
  );
}
