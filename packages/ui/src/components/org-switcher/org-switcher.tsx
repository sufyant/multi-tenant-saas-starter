"use client";

import type { ElementType, ReactNode } from "react";
import { cn } from "../../lib/utils";
import { CheckIcon, ChevronsUpDownIcon, Icon } from "../icon";
import { Label } from "../label";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";

export type SwitcherOrganisation = {
  id: string;
  name: string;
  /** The caller's role there, in the user's language: "Owner". */
  role: string;
  href: string;
  /** A Pill of a status that is not active, such as "Suspended". */
  status?: ReactNode;
};

export type OrgSwitcherProps = {
  /** The organisation of this page. */
  current: SwitcherOrganisation;
  /** Every organisation of the person, the current one included, in the order of the list. */
  organisations: SwitcherOrganisation[];
  /** The menu's heading: "Your organisations". */
  title: string;
  /** rail: the mark alone, as on a tablet's icon rail; row: the mark, the name and the role. */
  variant?: "row" | "rail";
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /** Previews only: render the menu open. */
  open?: boolean;
};

const Mark = ({ name }: { name: string }) => (
  <span
    aria-hidden="true"
    className="flex size-6 shrink-0 items-center justify-center rounded-md bg-primary text-small font-medium text-primary-foreground"
  >
    {name.charAt(0)}
  </span>
);

/**
 * The organisation switcher at the top of the sidebar (the Switcher screens): the organisation's mark, its name and the person's
 * role. With more than one organisation it opens "Your organisations", each with its role and a status that is not active, and a
 * click opens that organisation. With one organisation it shows no chevron and opens nothing.
 */
export function OrgSwitcher({
  current,
  organisations,
  title,
  variant = "row",
  linkComponent: Link = "a",
  open,
}: OrgSwitcherProps) {
  const several = organisations.length > 1;
  const face = (
    <>
      <Mark name={current.name} />
      <span
        className={cn(
          "flex min-w-0 flex-1 flex-col text-start",
          variant === "rail" && "sr-only",
        )}
      >
        <span className="truncate text-body font-medium">{current.name}</span>
        <span className="truncate text-small font-medium text-muted-foreground">
          {current.role}
        </span>
      </span>
    </>
  );
  const box = cn(
    "flex min-w-0 items-center gap-2.5 rounded-md",
    variant === "row" ? "h-11 w-full px-1.5" : "size-10 justify-center",
  );
  if (!several) return <div className={box}>{face}</div>;
  return (
    <DropdownMenu defaultOpen={open}>
      <DropdownMenuTrigger
        className={cn(
          box,
          "outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring data-popup-open:bg-accent",
        )}
      >
        {face}
        {variant === "row" && (
          <Icon
            icon={ChevronsUpDownIcon}
            size={14}
            className="text-muted-foreground"
          />
        )}
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-72 max-w-[calc(100vw-var(--space-8))]">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="text-small text-muted-foreground">
            {title}
          </DropdownMenuLabel>
          {organisations.map((organisation) => (
            <DropdownMenuItem
              key={organisation.id}
              render={<Link href={organisation.href} />}
              aria-current={organisation.id === current.id ? "page" : undefined}
              className="min-h-10 gap-2.5 px-2 max-tablet:min-h-touch-target"
            >
              <Mark name={organisation.name} />
              <bdi className="min-w-0 flex-1 truncate">{organisation.name}</bdi>
              {organisation.status}
              <Label>{organisation.role}</Label>
              {organisation.id === current.id && (
                <Icon icon={CheckIcon} size={14} />
              )}
            </DropdownMenuItem>
          ))}
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
