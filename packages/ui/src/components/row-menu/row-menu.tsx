"use client";

import { Fragment } from "react";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import { Icon, MoreHorizontalIcon } from "../icon";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";

export type RowMenuItem =
  | { separator: true }
  | {
      separator?: false;
      label: string;
      onSelect?: () => void;
      disabled?: boolean;
      /** Shown under a disabled item: why it cannot be used. */
      reason?: string;
      tone?: "default" | "destructive";
    };

export type RowMenuProps = {
  /** Safe actions first, destructive actions last after a separator. Leave out what the user may not do. */
  items: RowMenuItem[];
  /** The accessible name of the button: "Actions for Ayşe Yılmaz". */
  label: string;
  /** Previews only: render the menu open. */
  open?: boolean;
};

const labelBefore = (items: RowMenuItem[], index: number) => {
  const before = items[index - 1];
  return before && !before.separator ? before.label : "start";
};

/** The one ⋯ button of a table row or phone card (the RowMenu README). A disabled item stays and says why. */
export function RowMenu({ items, label, open }: RowMenuProps) {
  return (
    <DropdownMenu defaultOpen={open}>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            aria-label={label}
            icon={<Icon icon={MoreHorizontalIcon} />}
          />
        }
      />
      <DropdownMenuContent align="end" className="w-64">
        {items.map((item, index) =>
          item.separator ? (
            <DropdownMenuSeparator
              key={`separator-after-${labelBefore(items, index)}`}
            />
          ) : (
            <Fragment key={item.label}>
              <DropdownMenuItem
                variant={item.tone}
                disabled={item.disabled}
                onClick={item.onSelect}
                className={cn(
                  "flex-col items-start gap-0.5 py-1.5",
                  item.disabled && "data-disabled:opacity-100",
                )}
              >
                <span className={cn(item.disabled && "opacity-50")}>
                  {item.label}
                </span>
                {item.disabled && item.reason && (
                  <span className="text-small text-muted-foreground">
                    {item.reason}
                  </span>
                )}
              </DropdownMenuItem>
            </Fragment>
          ),
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
