"use client";

import { Combobox as ComboboxPrimitive } from "@base-ui/react";
import { type ReactNode, useId } from "react";
import { cn } from "../../lib/utils";
import { ChevronsUpDownIcon, Icon, type LucideIcon } from "../icon";
import {
  ComboboxContent,
  ComboboxEmpty,
  ComboboxItem,
  ComboboxList,
} from "../ui/combobox";

export type ComboboxOption = {
  id: string;
  /** Written in its own language and script for a language: "Türkçe", "العربية". */
  label: string;
  /** Shown at the end, such as the date format a language brings: "16/10/2026". */
  hint?: string;
  lang?: string;
  dir?: "ltr" | "rtl";
  /** Other words that find the option, such as its name in the current UI language. */
  keywords?: string[];
};

export type ComboboxProps = {
  label: ReactNode;
  /** Help under the field. An error replaces it. */
  hint?: ReactNode;
  /** What went wrong and how to fix it. */
  error?: ReactNode;
  value?: string;
  options: ComboboxOption[];
  onValueChange?: (id: string) => void;
  /** field (default) in forms; compact in the sign-in footer, with an icon and the name only. */
  variant?: "field" | "compact";
  icon?: LucideIcon;
  searchPlaceholder: string;
  /** Shown when the search matches no option. */
  emptyLabel: string;
  /** Previews only: render the list open. */
  open?: boolean;
  className?: string;
};

const matches = (option: ComboboxOption, query: string) => {
  const search = query.trim().toLocaleLowerCase();
  return [option.label, ...(option.keywords ?? [])].some((word) =>
    word.toLocaleLowerCase().includes(search),
  );
};

/**
 * A searchable picker for a long list (the Combobox README): the search box is the first thing in the list, and each option keeps
 * its own language and direction. New options never change the layout.
 */
export function Combobox({
  label,
  hint,
  error,
  value,
  options,
  onValueChange,
  variant = "field",
  icon,
  searchPlaceholder,
  emptyLabel,
  open,
  className,
}: ComboboxProps) {
  const labelId = useId();
  const noteId = useId();
  const note = error ?? hint;
  const selected = options.find((option) => option.id === value) ?? null;
  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <span
        id={labelId}
        className={cn(
          "text-body font-medium",
          variant === "compact" && "sr-only",
        )}
      >
        {label}
      </span>
      <ComboboxPrimitive.Root
        items={options}
        value={selected}
        defaultOpen={open}
        onValueChange={(option: ComboboxOption | null) =>
          option && onValueChange?.(option.id)
        }
        itemToStringLabel={(option: ComboboxOption) => option.label}
        itemToStringValue={(option: ComboboxOption) => option.id}
        filter={matches}
      >
        <ComboboxPrimitive.Trigger
          aria-labelledby={labelId}
          aria-invalid={error ? true : undefined}
          aria-describedby={note ? noteId : undefined}
          className={cn(
            "inline-flex items-center gap-2 rounded-md text-body outline-none focus-visible:ring-2 focus-visible:ring-ring",
            variant === "field"
              ? "h-control-md w-full justify-between border border-input bg-card px-3 pointer-coarse:h-control-lg tablet:pointer-coarse:h-control-xl"
              : "h-control-md px-2 text-muted-foreground hover:bg-accent pointer-coarse:h-control-lg",
          )}
        >
          {icon && <Icon icon={icon} />}
          <span lang={selected?.lang} dir={selected?.dir} className="truncate">
            {selected?.label}
          </span>
          {variant === "field" && (
            <Icon icon={ChevronsUpDownIcon} className="text-muted-foreground" />
          )}
        </ComboboxPrimitive.Trigger>
        <ComboboxContent className="min-w-64">
          <ComboboxPrimitive.Input
            placeholder={searchPlaceholder}
            aria-label={searchPlaceholder}
            className="m-1 h-control-md w-[calc(100%-0.5rem)] rounded-md border border-input bg-card px-3 text-body outline-none"
          />
          <ComboboxEmpty>{emptyLabel}</ComboboxEmpty>
          <ComboboxList>
            {(option: ComboboxOption) => (
              <ComboboxItem
                key={option.id}
                value={option}
                className="min-h-control-md pointer-coarse:min-h-control-lg"
              >
                <span lang={option.lang} dir={option.dir} className="flex-1">
                  {option.label}
                </span>
                {option.hint && (
                  <span
                    dir="ltr"
                    className="text-small text-muted-foreground tabular-nums"
                  >
                    {option.hint}
                  </span>
                )}
              </ComboboxItem>
            )}
          </ComboboxList>
        </ComboboxContent>
      </ComboboxPrimitive.Root>
      {note && (
        <p
          id={noteId}
          className={cn(
            "text-small",
            error ? "text-destructive-text" : "text-muted-foreground",
          )}
        >
          {note}
        </p>
      )}
    </div>
  );
}
