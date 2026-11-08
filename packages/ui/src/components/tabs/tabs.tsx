"use client";

import { Tabs as TabsBase, TabsList, TabsTrigger } from "../ui/tabs";

export type TabsItem = { id: string; label: string; count?: number };

export type TabsProps = {
  /** Two to five views of one subject. */
  items: TabsItem[];
  current: string;
  /** The accessible name of the tab list. */
  label: string;
  /** The app keeps the current tab in the URL (F9). */
  onValueChange?: (id: string) => void;
};

/**
 * Switches between views of one subject on the same page (the Tabs README and the Tabs component, 425:2523): a row across the
 * content's full width with one line under it, the current tab's 2 px line on that line. It never navigates to another section.
 */
export function Tabs({ items, current, label, onValueChange }: TabsProps) {
  return (
    <TabsBase
      value={current}
      onValueChange={(value) => onValueChange?.(String(value))}
    >
      <TabsList
        variant="line"
        aria-label={label}
        className="h-auto w-full justify-start gap-5 border-b border-border p-0"
      >
        {items.map((item) => (
          <TabsTrigger
            key={item.id}
            value={item.id}
            className="h-auto flex-none rounded-none border-0 px-0 pt-0.5 pb-2.25 text-body font-normal text-muted-foreground group-data-horizontal/tabs:after:-bottom-px pointer-coarse:min-h-control-lg data-active:font-medium data-active:text-foreground"
          >
            {item.label}
            {item.count !== undefined && " "}
            {item.count !== undefined && (
              <span className="text-status text-muted-foreground tabular-nums">
                {item.count}
              </span>
            )}
          </TabsTrigger>
        ))}
      </TabsList>
    </TabsBase>
  );
}
