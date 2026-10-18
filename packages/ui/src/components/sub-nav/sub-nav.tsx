import type { ElementType } from "react";
import { cn } from "../../lib/utils";

export type SubNavItem = { id: string; label: string; href: string };
export type SubNavGroup = { label?: string; items: SubNavItem[] };

export type SubNavProps = {
  /** Organisation (General, Members, Activity) and You (Preferences). Leave out what the user may not see. */
  groups: SubNavGroup[];
  current: string;
  /** The accessible name of the navigation. */
  label: string;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
};

/** The second level inside Settings (the SubNav README): a column on wide screens, a scrolling row of tabs on a phone. */
export function SubNav({
  groups,
  current,
  label,
  linkComponent: Link = "a",
}: SubNavProps) {
  return (
    <nav
      aria-label={label}
      className="flex gap-4 overflow-x-auto tablet:w-[200px] tablet:flex-col tablet:overflow-visible"
    >
      {groups.map((group, index) => (
        <div
          key={group.label ?? index}
          className="flex shrink-0 gap-1 tablet:flex-col"
        >
          {group.label && (
            <p className="hidden px-3 pb-1 text-small text-muted-foreground tablet:block">
              {group.label}
            </p>
          )}
          {group.items.map((item) => (
            <Link
              key={item.id}
              href={item.href}
              aria-current={item.id === current ? "page" : undefined}
              className={cn(
                "flex h-control-md items-center rounded-md px-3 text-body whitespace-nowrap pointer-coarse:h-control-lg hover:bg-accent",
                item.id === current &&
                  "bg-primary-subtle font-medium text-primary-subtle-foreground",
              )}
            >
              {item.label}
            </Link>
          ))}
        </div>
      ))}
    </nav>
  );
}
