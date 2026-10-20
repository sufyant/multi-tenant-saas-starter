"use client";

import {
  type ElementType,
  Fragment,
  type ReactNode,
  useEffect,
  useState,
} from "react";
import { railCookieOf } from "../../lib/rail-state";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import {
  ChevronRightIcon,
  Icon,
  type LucideIcon,
  MenuIcon,
  PanelLeftCloseIcon,
  PanelLeftOpenIcon,
  SearchIcon,
} from "../icon";
import {
  Command,
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "../ui/command";
import { Sheet, SheetContent, SheetTitle } from "../ui/sheet";
import { Tooltip, TooltipContent, TooltipTrigger } from "../ui/tooltip";

export type NavItem = {
  id: string;
  label: string;
  icon: LucideIcon;
  href: string;
  /** bottom puts the item at the foot of the rail. Settings lives there. */
  placement?: "top" | "bottom";
};

export type Crumb = { label: string; href?: string };

export type AppShellLabels = {
  /** The accessible name of the rail and the drawer. */
  navigation: string;
  /** The phone's menu button. */
  menu: string;
  /** The search field. */
  search: string;
  /** The keyboard hint of the search field: "⌘K", or "Ctrl K" off Apple devices. */
  searchShortcut: string;
  breadcrumb: string;
  /** The command palette's title, placeholder and empty text. */
  commandTitle: string;
  commandPlaceholder: string;
  commandEmpty: string;
  /** The drawer's close button. */
  close: string;
  /** The rail's toggle on desktop. Its name stays; `aria-expanded` says the state. */
  railToggle: string;
  /** The keyboard hint of the toggle: "⌘B", or "Ctrl B" off Apple devices. */
  railShortcut: string;
};

export type AppShellProps = {
  nav: NavItem[];
  current: string;
  /** The product mark in the logo tile, and its accessible name. */
  logo: ReactNode;
  logoLabel: string;
  logoHref: string;
  /** The breadcrumb. The first crumb is the organisation; `orgMenu` takes its place when the user has more than one. */
  crumbs: Crumb[];
  orgMenu?: ReactNode;
  /** The platform app's ink label. It starts the top bar on every page. */
  platformLabel?: string;
  userMenu: ReactNode;
  labels: AppShellLabels;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /** Goes to a page chosen in the command palette. */
  onNavigate: (href: string) => void;
  /** The rail shows its labels on desktop (F49). The app's layout reads it from the cookie on the server, so the first paint is right. */
  railExpanded?: boolean;
  /** Previews only: render the phone drawer open. */
  drawerOpen?: boolean;
  children: ReactNode;
};

/** A key press in a text field belongs to the field, so ⌘B there never moves the rail. */
const typing = (target: EventTarget | null) =>
  target instanceof HTMLElement &&
  (target.isContentEditable ||
    ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));

/** Flips the rail and saves the choice: the one write of the cookie that each app's layout reads (shadcn/ui, "Sidebar"). */
const flipRail = (open: boolean) => {
  // biome-ignore lint/suspicious/noDocumentCookie: the Cookie Store API is missing from jsdom and from Safari before 18.4.
  document.cookie = railCookieOf(!open);
  return !open;
};

/**
 * The frame of every signed-in page (the AppShell README): an icon rail at the start edge, a top bar with the breadcrumb, search
 * (⌘K) and the user's avatar, and the content. From bp-desktop the rail expands to show its labels: the toggle at its foot or ⌘B,
 * saved in a cookie (F49). On a tablet it stays the icon rail; under bp-tablet it becomes a drawer from the start edge. The whole
 * shell mirrors in right-to-left.
 */
export function AppShell({
  nav,
  current,
  logo,
  logoLabel,
  logoHref,
  crumbs,
  orgMenu,
  platformLabel,
  userMenu,
  labels,
  linkComponent: Link = "a",
  onNavigate,
  railExpanded = false,
  drawerOpen = false,
  children,
}: AppShellProps) {
  const [drawer, setDrawer] = useState(drawerOpen);
  const [palette, setPalette] = useState(false);
  const [expanded, setExpanded] = useState(railExpanded);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (!(event.metaKey || event.ctrlKey)) return;
      const key = event.key.toLowerCase();
      if (key === "k") {
        event.preventDefault();
        setPalette((open) => !open);
      }
      if (key === "b" && !typing(event.target)) {
        event.preventDefault();
        setExpanded(flipRail);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  // Expanded, the label shows from bp-desktop and the tooltip hides there; a tablet keeps the icon rail and its tooltips.
  const railItem = (item: NavItem) => (
    <Tooltip key={item.id}>
      <TooltipTrigger
        render={<Link href={item.href} />}
        aria-label={item.label}
        aria-current={item.id === current ? "page" : undefined}
        className={cn(
          "flex size-10 items-center justify-center rounded-md text-sidebar-foreground hover:bg-accent",
          expanded &&
            "desktop:h-10 desktop:w-full desktop:justify-start desktop:gap-3 desktop:px-3",
          item.id === current &&
            "bg-sidebar-accent text-sidebar-accent-foreground shadow-xs",
        )}
      >
        <Icon icon={item.icon} size={18} />
        {expanded && (
          <span className="hidden truncate text-body desktop:inline">
            {item.label}
          </span>
        )}
      </TooltipTrigger>
      <TooltipContent
        side="inline-end"
        className={cn(expanded && "desktop:hidden")}
      >
        {item.label}
      </TooltipContent>
    </Tooltip>
  );

  const drawerItem = (item: NavItem) => (
    <Link
      key={item.id}
      href={item.href}
      onClick={() => setDrawer(false)}
      aria-current={item.id === current ? "page" : undefined}
      className={cn(
        "flex h-touch-target items-center gap-3 rounded-md px-3 text-body tablet:h-touch-target-tablet",
        item.id === current &&
          "bg-sidebar-accent font-medium text-sidebar-accent-foreground",
      )}
    >
      <Icon icon={item.icon} size={18} />
      {item.label}
    </Link>
  );

  const top = nav.filter((item) => item.placement !== "bottom");
  const bottom = nav.filter((item) => item.placement === "bottom");
  const [first, ...rest] = crumbs;

  return (
    <div className="flex min-h-dvh bg-background text-foreground">
      <nav
        aria-label={labels.navigation}
        className={cn(
          "sticky top-0 hidden h-dvh w-rail-width shrink-0 flex-col items-center gap-1 border-e border-sidebar-border bg-sidebar py-2 tablet:flex",
          expanded &&
            "desktop:w-rail-expanded desktop:items-stretch desktop:px-2",
        )}
      >
        <Link
          href={logoHref}
          aria-label={logoLabel}
          className="mb-2 flex size-9 items-center justify-center rounded-lg bg-primary font-semibold text-primary-foreground"
        >
          {logo}
        </Link>
        {top.map(railItem)}
        <div className="mt-auto flex flex-col gap-1">
          {bottom.map(railItem)}
          <Tooltip>
            <TooltipTrigger
              render={
                <button
                  type="button"
                  aria-expanded={expanded}
                  aria-label={labels.railToggle}
                  onClick={() => setExpanded(flipRail)}
                />
              }
              className={cn(
                "hidden size-10 items-center justify-center rounded-md text-muted-foreground hover:bg-accent desktop:flex",
                expanded && "desktop:w-full desktop:justify-start desktop:px-3",
              )}
            >
              <Icon
                icon={expanded ? PanelLeftCloseIcon : PanelLeftOpenIcon}
                size={18}
                directional
              />
            </TooltipTrigger>
            <TooltipContent side="inline-end">
              {labels.railToggle}{" "}
              <kbd dir="ltr" className="font-mono text-mono">
                {labels.railShortcut}
              </kbd>
            </TooltipContent>
          </Tooltip>
        </div>
      </nav>

      <Sheet open={drawer} onOpenChange={setDrawer}>
        <SheetContent
          side="inline-start"
          closeLabel={labels.close}
          className="w-72 bg-sidebar p-3"
        >
          <SheetTitle className="sr-only">{labels.navigation}</SheetTitle>
          <nav
            aria-label={labels.navigation}
            className="flex flex-1 flex-col gap-1 pt-10"
          >
            {top.map(drawerItem)}
            <div className="mt-auto flex flex-col gap-1">
              {bottom.map(drawerItem)}
            </div>
          </nav>
        </SheetContent>
      </Sheet>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-sticky flex h-[52px] items-center gap-2 border-b border-border bg-background px-4 tablet:px-6">
          <Button
            variant="ghost"
            className="tablet:hidden"
            aria-label={labels.menu}
            icon={<Icon icon={MenuIcon} />}
            onClick={() => setDrawer(true)}
          />
          {platformLabel && (
            <span className="rounded-sm bg-platform px-1.5 py-0.5 text-status text-platform-foreground">
              {platformLabel}
            </span>
          )}
          <nav aria-label={labels.breadcrumb} className="min-w-0 flex-1">
            <ol className="flex items-center gap-1 text-body">
              {first && (
                <li className="flex items-center">
                  {orgMenu ?? (
                    <span className="px-1.5 font-medium">{first.label}</span>
                  )}
                </li>
              )}
              {rest.map((crumb, index) => (
                <Fragment key={crumb.href ?? crumb.label}>
                  <li
                    aria-hidden="true"
                    className="hidden text-muted-foreground tablet:flex"
                  >
                    <Icon icon={ChevronRightIcon} directional />
                  </li>
                  <li
                    aria-current={
                      index === rest.length - 1 ? "page" : undefined
                    }
                    className="hidden truncate text-muted-foreground last:text-foreground tablet:block"
                  >
                    {crumb.href ? (
                      <Link href={crumb.href}>{crumb.label}</Link>
                    ) : (
                      crumb.label
                    )}
                  </li>
                </Fragment>
              ))}
            </ol>
          </nav>
          <button
            type="button"
            onClick={() => setPalette(true)}
            className="hidden h-control-md w-56 items-center gap-2 rounded-md border border-input bg-card px-3 text-body text-muted-foreground shadow-xs tablet:flex"
          >
            <Icon icon={SearchIcon} />
            <span className="flex-1 text-start">{labels.search}</span>
            <kbd
              dir="ltr"
              className="rounded-sm border border-border px-1 font-mono text-mono"
            >
              {labels.searchShortcut}
            </kbd>
          </button>
          <Button
            variant="ghost"
            className="tablet:hidden"
            aria-label={labels.search}
            icon={<Icon icon={SearchIcon} />}
            onClick={() => setPalette(true)}
          />
          {userMenu}
        </header>
        <main className="mx-auto w-full max-w-content-max flex-1 px-4 py-6 tablet:px-6">
          {children}
        </main>
      </div>

      <CommandDialog
        open={palette}
        onOpenChange={setPalette}
        title={labels.commandTitle}
      >
        <Command>
          <CommandInput placeholder={labels.commandPlaceholder} />
          <CommandList>
            <CommandEmpty>{labels.commandEmpty}</CommandEmpty>
            <CommandGroup>
              {nav.map((item) => (
                <CommandItem
                  key={item.id}
                  value={item.label}
                  onSelect={() => {
                    setPalette(false);
                    onNavigate(item.href);
                  }}
                >
                  <Icon icon={item.icon} />
                  {item.label}
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </CommandDialog>
    </div>
  );
}
