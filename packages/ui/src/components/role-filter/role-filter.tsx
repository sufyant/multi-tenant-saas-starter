"use client";

import { usePhone } from "../../lib/use-phone";
import { Button } from "../button";
import { Icon, ListFilterIcon } from "../icon";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";

export type RoleFilterOption = { id: string; label: string };

export type RoleFilterProps = {
  /** What it filters: "Filter by role". The button's name is this and the chosen role (WCAG 2.5.3 keeps the visible text in it). */
  label: string;
  /** The button's text on a phone: "Filter". */
  phoneLabel: string;
  value: string;
  /** "All roles" first, then the roles in the list's order. */
  options: RoleFilterOption[];
  onValueChange: (id: string) => void;
};

/**
 * Filters a list by role (the Role filter component, 425:2592): a secondary button with `list-filter` and the chosen role, which
 * opens a 240 px menu under it at its start, the chosen role checked. A menu, not a Combobox: the list is short and needs no
 * typing.
 */
export function RoleFilter({
  label,
  phoneLabel,
  value,
  options,
  onValueChange,
}: RoleFilterProps) {
  const phone = usePhone();
  const chosen = options.find((option) => option.id === value)?.label ?? "";
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            aria-label={`${label}: ${chosen}`}
            icon={<Icon icon={ListFilterIcon} />}
          >
            {phone ? phoneLabel : chosen}
          </Button>
        }
      />
      <DropdownMenuContent align="start" aria-label={label} className="w-60">
        <DropdownMenuRadioGroup
          value={value}
          onValueChange={(id) => onValueChange(String(id))}
        >
          {options.map((option) => (
            <DropdownMenuRadioItem
              key={option.id}
              value={option.id}
              className="min-h-8 gap-2.5 px-2.5 pe-8 text-small [&_[data-slot=dropdown-menu-radio-item-indicator]]:end-2.5 [&_[data-slot=dropdown-menu-radio-item-indicator]_svg]:text-muted-foreground"
            >
              {option.label}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
