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

/** Switches between views of one subject on the same page (the Tabs README). It never navigates to another section. */
export function Tabs({ items, current, label, onValueChange }: TabsProps) {
  return (
    <TabsBase
      value={current}
      onValueChange={(value) => onValueChange?.(String(value))}
    >
      <TabsList
        variant="line"
        aria-label={label}
        className="h-auto gap-4 border-b border-border p-0"
      >
        {items.map((item) => (
          <TabsTrigger
            key={item.id}
            value={item.id}
            className="h-control-md flex-none rounded-none px-0 pointer-coarse:h-control-lg data-active:after:bg-primary"
          >
            {item.label}
            {item.count !== undefined && " "}
            {item.count !== undefined && (
              <span className="text-muted-foreground tabular-nums">
                {item.count}
              </span>
            )}
          </TabsTrigger>
        ))}
      </TabsList>
    </TabsBase>
  );
}
