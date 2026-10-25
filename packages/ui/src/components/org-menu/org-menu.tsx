"use client";

import type { ElementType } from "react";
import { ChevronsUpDownIcon, Icon } from "../icon";
import { Tag } from "../tag";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";

export type OrgMenuProps = {
  name: string;
  slug: string;
  initials: string;
  /** The caller's role here, in the user's language: "Owner". */
  memberRole: string;
  /** "Switch organisation". */
  switchLabel: string;
  /** The Choose an organisation page. */
  switchHref: string;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /** Previews only: render the menu open. */
  open?: boolean;
};

/**
 * Opens from the organisation name, the first crumb, when the user has more than one organisation (the OrgMenu README). Its one
 * action goes to the Choose an organisation page: switching is a deliberate step.
 */
export function OrgMenu({
  name,
  slug,
  initials,
  memberRole,
  switchLabel,
  switchHref,
  linkComponent: Link = "a",
  open,
}: OrgMenuProps) {
  return (
    <DropdownMenu defaultOpen={open}>
      <DropdownMenuTrigger className="inline-flex h-control-md items-center gap-1 rounded-md px-1.5 font-medium outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring pointer-coarse:h-control-lg">
        {name}
        <Icon icon={ChevronsUpDownIcon} className="text-muted-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-72">
        <div className="flex items-center gap-3 px-2 py-2">
          <span
            aria-hidden="true"
            className="flex size-8 items-center justify-center rounded-lg bg-primary font-semibold text-primary-foreground"
          >
            {initials}
          </span>
          <div className="flex min-w-0 flex-1 flex-col">
            <span className="truncate font-medium">{name}</span>
            <bdi
              dir="ltr"
              className="truncate font-mono text-mono text-muted-foreground"
            >
              {slug}
            </bdi>
          </div>
          <Tag variant="text">{memberRole}</Tag>
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem render={<Link href={switchHref} />}>
          {switchLabel}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
