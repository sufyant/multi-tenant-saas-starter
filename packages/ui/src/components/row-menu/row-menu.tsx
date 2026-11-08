"use client";

import { Fragment } from "react";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import { Icon, type LucideIcon, MoreHorizontalIcon } from "../icon";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";

export type RowMenuItem = {
  label: string;
  /** The 16 px icon before the label, where the item's frame draws one. */
  icon?: LucideIcon;
  onSelect?: () => void;
  disabled?: boolean;
  /** Shown under a disabled item: why it cannot be used. */
  reason?: string;
  tone?: "default" | "destructive";
};

export type RowMenuProps = {
  /** Safe actions first, destructive actions last; the menu puts a separator between the two groups. Leave out what the user may not do. */
  items: RowMenuItem[];
  /** The accessible name of the button: "Actions for Ayşe Yılmaz". */
  label: string;
  /** Previews only: render the menu open. */
  open?: boolean;
  /** Which edge of the button the menu lines up with. A row's menu ends at its button; a page header's opens from its start (8.41). */
  align?: "start" | "end";
};

/**
 * The one ⋯ button of a table row or phone card (the RowMenu component, 406:2426): 240 wide, 32 px items with a 16 px icon, and
 * a separator before the destructive group, none when every item is destructive (Apple HIG, Menus). A disabled item stays and
 * says why.
 */
export function RowMenu({ items, label, open, align = "end" }: RowMenuProps) {
  const firstDestructive = items.findIndex(
    (item) => item.tone === "destructive",
  );
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
      <DropdownMenuContent align={align} className="w-60">
        {items.map((item, index) => (
          <Fragment key={item.label}>
            {index > 0 && index === firstDestructive && (
              <DropdownMenuSeparator className="mx-0" />
            )}
            <DropdownMenuItem
              variant={item.tone}
              disabled={item.disabled}
              onClick={item.onSelect}
              className={cn(
                "data-[variant=destructive]:text-destructive-text data-[variant=destructive]:*:[svg]:text-destructive-text",
                item.reason && item.disabled && "items-start py-1.5",
                item.disabled && "data-disabled:opacity-100",
              )}
            >
              {item.icon && (
                <Icon
                  icon={item.icon}
                  className={cn(
                    item.tone !== "destructive" && "text-muted-foreground",
                    item.disabled && "opacity-50",
                  )}
                />
              )}
              <span className="flex flex-col gap-0.5">
                <span className={cn(item.disabled && "opacity-50")}>
                  {item.label}
                </span>
                {item.disabled && item.reason && (
                  <span className="text-small text-muted-foreground">
                    {item.reason}
                  </span>
                )}
              </span>
            </DropdownMenuItem>
          </Fragment>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
