"use client";

import type { ElementType, ReactNode } from "react";
import { cn } from "../../lib/utils";
import { CheckIcon, ChevronsUpDownIcon, Icon } from "../icon";
import { OrgTile } from "../org-tile";
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
  /**
   * rail: the sidebar's place, the mark alone on a tablet's icon rail and the whole row from bp-desktop; row: always the whole row;
   * drawer: the phone's drawer, the organisation's name as the menu's heading, and the whole row only to switch between several.
   */
  variant?: "row" | "rail" | "drawer";
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /** Previews only: render the menu open. */
  open?: boolean;
  /** The face names the product, not an organisation (the platform): its mark keeps the brand's gradient. */
  product?: boolean;
};

/** The product's mark: its first letter on the brand's gradient (the Platform sidebar). */
const ProductMark = ({ name }: { name: string }) => (
  <span
    aria-hidden="true"
    className="flex size-6 shrink-0 items-center justify-center rounded-md bg-linear-135 from-primary to-brand-gradient-end to-50% text-small font-medium text-primary-foreground"
  >
    {name.charAt(0)}
  </span>
);

/**
 * The organisation switcher at the top of the sidebar (the Switcher screens): the organisation's mark, its name and the person's
 * role. With more than one organisation it opens "Your organisations", each with its role under its name and a status that is not
 * active, and a click opens that organisation. With one organisation it shows no chevron and opens nothing. In the sidebar's place
 * (`rail`) a tablet's icon rail shows the mark alone and the expanded sidebar from bp-desktop the whole row.
 */
export function OrgSwitcher({
  current,
  organisations,
  title,
  variant = "row",
  linkComponent: Link = "a",
  open,
  product,
}: OrgSwitcherProps) {
  const several = organisations.length > 1;
  const rail = variant === "rail";
  const face = (
    <>
      {product ? (
        <ProductMark name={current.name} />
      ) : (
        <OrgTile name={current.name} />
      )}
      <span
        className={cn(
          "flex min-w-0 flex-1 flex-col text-start",
          rail && "max-desktop:sr-only",
        )}
      >
        <span className="truncate text-body font-medium">{current.name}</span>
        <span className="truncate text-status text-muted-foreground">
          {current.role}
        </span>
      </span>
    </>
  );
  const box = cn(
    "flex w-full min-w-0 items-center gap-2.5 rounded-md px-1.5 py-1",
    rail && "max-desktop:size-12 max-desktop:justify-center max-desktop:p-0",
  );
  if (!several && variant === "drawer")
    return (
      <p className="truncate ps-2 pt-3.5 pb-1 text-status text-muted-foreground">
        <bdi>{current.name}</bdi>
      </p>
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
        <Icon
          icon={ChevronsUpDownIcon}
          size={14}
          className={cn(
            "shrink-0 text-muted-foreground",
            rail && "max-desktop:hidden",
          )}
        />
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-75 max-w-[calc(100vw-var(--space-8))]">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="ps-2.5 pt-1.5 pb-1 text-status text-muted-foreground">
            {title}
          </DropdownMenuLabel>
          {organisations.map((organisation) => (
            <DropdownMenuItem
              key={organisation.id}
              render={<Link href={organisation.href} />}
              aria-current={organisation.id === current.id ? "page" : undefined}
              className="min-h-10 gap-2.5 px-2 aria-[current=page]:bg-accent max-tablet:min-h-touch-target"
            >
              <OrgTile name={organisation.name} size="sm" />
              <span className="flex min-w-0 flex-1 flex-col">
                <bdi className="truncate text-small font-medium">
                  {organisation.name}
                </bdi>
                <span className="truncate text-status text-muted-foreground">
                  {organisation.role}
                </span>
              </span>
              {organisation.status}
              {organisation.id === current.id && (
                <Icon icon={CheckIcon} size={16} />
              )}
            </DropdownMenuItem>
          ))}
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
