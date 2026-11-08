"use client";

import { useTranslations } from "next-intl";
import {
  type ElementType,
  Fragment,
  type ReactNode,
  useEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { usePhone } from "../../lib/use-phone";
import { useTabTitle } from "../../lib/use-tab-title";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import {
  ArrowLeftIcon,
  Icon,
  type LucideIcon,
  MenuIcon,
  SearchIcon,
} from "../icon";
import { Truncate, useCut } from "../truncate";
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
  /** A count after the label, such as the members of the organisation. */
  count?: number;
};

/** A group of the settings layout: "My account", "Organisation". */
export type NavGroup = { id: string; label: string; items: NavItem[] };

export type Crumb = { label: string; href?: string };

export type CommandEntry = {
  id: string;
  label: string;
  icon?: LucideIcon;
  /** A page: the menu goes there. */
  href?: string;
  /** An action: the menu runs it. */
  onSelect?: () => void;
};

/** A group of the command menu: "Go to", "Settings · My account", "Actions". */
export type CommandSection = {
  id: string;
  label: string;
  items: CommandEntry[];
};

/** Where a part of the shell draws itself: the expanded sidebar or a tablet's icon rail, or the phone's top bar. */
export type Place = "row" | "rail";

export type AppShellProps = {
  /** The top of the sidebar: the organisation switcher in the tenant web, the product and "Platform" in the platform app. */
  head: (place: Place) => ReactNode;
  /** The main menu: Home and the product's sections in the tenant web; the platform's pages in the platform app. */
  main: NavItem[];
  /** Settings, at the foot of the main menu. */
  settings: NavItem;
  /** Inside Settings the sidebar shows the settings layout: "← Back to …" and its groups, in place of the main menu. */
  settingsMenu?: { back: { label: string; href: string }; groups: NavGroup[] };
  /** The id of the current page's item. */
  current: string;
  /** The notification bell: a row of the sidebar, an icon of the rail, an icon of the phone's top bar. */
  bell?: (place: Place | "icon") => ReactNode;
  /** The account menu at the foot of the sidebar and of the drawer. */
  account?: (place: Place) => ReactNode;
  /** The version under the account in the phone drawer: "Version a1b2c3d". */
  version?: string;
  /** The page's name: the title of the phone's top bar. */
  pageName: string;
  /** The breadcrumb of the top bar from bp-tablet. */
  crumbs: Crumb[];
  /** The command menu's groups (⌘K). */
  commands: CommandSection[];
  /** The one bar of the highest priority, such as the connection bar; the app chooses it (the Billing bar priority pattern). */
  bar?: ReactNode;
  /** The browser tab's title (`useTabTitle`). */
  tabTitle?: string;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /** Goes to a page chosen in the command menu. */
  onNavigate: (href: string) => void;
  /** Previews only: render the phone drawer open. */
  drawerOpen?: boolean;
  children: ReactNode;
};

const subscribeToNothing = () => () => {};
/** ⌘K on Apple devices, Ctrl K elsewhere. The server renders the Apple form. */
const useAppleKeys = () =>
  useSyncExternalStore(
    subscribeToNothing,
    () => /Mac|iPhone|iPad/.test(navigator.userAgent),
    () => true,
  );

const rowBase =
  "group/row flex h-7.5 items-center gap-2.5 rounded-md px-2 text-body outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring";
/** A tablet's icon rail: 48 px items with 18 px icons (target/touch-tablet). */
const railItem =
  "max-desktop:size-12 max-desktop:justify-center max-desktop:px-0";
/** The phone's drawer: 44 px rows with 18 px icons (target/touch). */
const drawerRow = "h-touch-target gap-3 px-2.5";

/**
 * The frame of every signed-in page (the Shell screens and the Screen sizes rules). From bp-desktop the sidebar is open with its
 * labels: the head, search (⌘K), Notifications, the main menu, then Settings and the account at its foot; inside Settings the
 * settings layout takes its place. On a tablet it is an icon rail, each label a tooltip. Under bp-tablet a top bar holds the menu
 * button, the page's name and the bell, and the menu opens as a drawer. The content sits in a card with the breadcrumb above it.
 * The environment strip above it belongs to the root (`AppFrameProvider`). Only ⌘K or Ctrl K is a shortcut. The whole shell mirrors in
 * right-to-left.
 */
export function AppShell({
  head,
  main,
  settings,
  settingsMenu,
  current,
  bell,
  account,
  version,
  pageName,
  crumbs,
  commands,
  bar,
  tabTitle,
  linkComponent: Link = "a",
  onNavigate,
  drawerOpen = false,
  children,
}: AppShellProps) {
  const t = useTranslations("appShell");
  const phone = usePhone();
  const apple = useAppleKeys();
  const [drawer, setDrawer] = useState(drawerOpen);
  const [palette, setPalette] = useState(false);
  useTabTitle(tabTitle);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (!(event.metaKey || event.ctrlKey) || event.key.toLowerCase() !== "k")
        return;
      event.preventDefault();
      setPalette((open) => !open);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  const shortcut = apple ? t("searchShortcutMac") : t("searchShortcutOther");

  const item = (entry: NavItem, place: Place | "drawer") => (
    <MenuLink
      key={entry.id}
      entry={entry}
      place={place}
      active={entry.id === current}
      linkComponent={Link}
      onFollow={place === "drawer" ? () => setDrawer(false) : undefined}
    />
  );

  const search = (place: Place | "drawer") => {
    const button = (
      <button
        type="button"
        aria-label={t("search")}
        aria-keyshortcuts={place === "drawer" ? undefined : "Meta+K Control+K"}
        onClick={() => {
          setDrawer(false);
          setPalette(true);
        }}
        className={cn(
          "flex h-7.5 w-full min-w-0 items-center gap-2 rounded-md border border-border bg-card px-2 text-small text-muted-foreground outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring",
          place === "rail" &&
            "max-desktop:size-12 max-desktop:justify-center max-desktop:border-0 max-desktop:bg-transparent max-desktop:px-0",
          place === "drawer" && "h-touch-target",
        )}
      >
        <Icon
          icon={SearchIcon}
          size={14}
          className={cn("shrink-0", place === "rail" && "max-desktop:size-4.5")}
        />
        <span
          aria-hidden="true"
          className={cn(
            "min-w-0 flex-1 truncate text-start",
            place === "rail" && "max-desktop:hidden",
          )}
        >
          {t("search")}
        </span>
      </button>
    );
    if (place === "drawer") return button;
    return (
      <Tooltip>
        <TooltipTrigger render={button} />
        <TooltipContent side="inline-end">
          {t("search")}{" "}
          <kbd dir="ltr" className="font-mono text-mono">
            {shortcut}
          </kbd>
        </TooltipContent>
      </Tooltip>
    );
  };

  /** The sidebar's column: the main menu, or the settings layout inside Settings. */
  const menu = (place: Place | "drawer") => {
    const wide: Place = place === "rail" ? "rail" : "row";
    if (settingsMenu)
      return (
        <>
          {item(
            {
              id: "back",
              label: settingsMenu.back.label,
              href: settingsMenu.back.href,
              icon: ArrowLeftIcon,
            },
            place,
          )}
          {settingsMenu.groups.map((group) => (
            <div key={group.id} className="flex flex-col gap-0.5">
              {place === "rail" && (
                <span
                  aria-hidden="true"
                  className="mx-auto my-1 h-px w-6 bg-border desktop:hidden"
                />
              )}
              <span
                className={cn(
                  "px-2 pt-3.5 pb-1 text-status text-muted-foreground",
                  place === "rail" && "max-desktop:sr-only",
                )}
              >
                {group.label}
              </span>
              {group.items.map((entry) => item(entry, place))}
            </div>
          ))}
          <div className="mt-auto flex flex-col gap-0.5 pt-2">
            {account?.(wide)}
          </div>
        </>
      );
    return (
      <>
        {head(wide)}
        <div className="mt-2.5 flex flex-col gap-0.5">
          {search(place)}
          {place !== "drawer" && bell?.(wide)}
        </div>
        <div className="mt-2.5 flex flex-col gap-0.5">
          {main.map((entry) => item(entry, place))}
        </div>
        <div className="mt-auto flex flex-col gap-0.5 pt-2">
          {item(settings, place)}
          {account?.(wide)}
        </div>
      </>
    );
  };

  return (
    <div className="flex min-h-[calc(100dvh-var(--strip-height,0px))] flex-1 flex-col bg-sidebar text-foreground">
      <div className="flex flex-1">
        {!phone && (
          <nav
            aria-label={
              settingsMenu ? t("settingsNavigation") : t("navigation")
            }
            className="sticky top-(--strip-height) hidden h-[calc(100dvh-var(--strip-height))] w-rail-width shrink-0 flex-col gap-0.5 overflow-y-auto px-1 py-3 tablet:flex desktop:w-rail-expanded desktop:px-2.5"
          >
            {menu("rail")}
          </nav>
        )}

        {phone && (
          <Sheet open={drawer} onOpenChange={setDrawer}>
            <SheetContent
              side="inline-start"
              className="w-75 gap-0 bg-sidebar px-3 pt-[max(var(--space-3),env(safe-area-inset-top))] pb-[max(var(--space-4),env(safe-area-inset-bottom))]"
            >
              <SheetTitle className="sr-only">
                {settingsMenu ? t("settingsNavigation") : t("navigation")}
              </SheetTitle>
              <nav
                aria-label={
                  settingsMenu ? t("settingsNavigation") : t("navigation")
                }
                className="flex flex-1 flex-col gap-0.5 overflow-y-auto"
              >
                {menu("drawer")}
              </nav>
              {version && (
                <p className="flex h-7 items-center px-2 text-status font-normal text-muted-foreground">
                  {t("version", { version })}
                </p>
              )}
            </SheetContent>
          </Sheet>
        )}

        <div className="flex min-w-0 flex-1 flex-col tablet:py-2 tablet:pe-2">
          <div className="flex min-w-0 flex-1 flex-col bg-card tablet:overflow-clip tablet:rounded-lg tablet:border tablet:border-border tablet:shadow-md">
            {phone && (
              <header className="sticky top-(--strip-height) z-sticky flex h-[52px] shrink-0 items-center gap-1 border-b border-border bg-card px-1">
                <Button
                  variant="ghost"
                  className="size-touch-target"
                  aria-label={t("openMenu")}
                  icon={<Icon icon={MenuIcon} size={20} />}
                  onClick={() => setDrawer(true)}
                />
                <h1 className="min-w-0 flex-1 truncate text-center text-section-title">
                  {pageName}
                </h1>
                {bell?.("icon")}
              </header>
            )}
            {bar}
            {!phone && (
              <nav
                aria-label={t("breadcrumb")}
                className="flex h-12 shrink-0 items-center border-b border-border px-5"
              >
                <ol className="flex min-w-0 items-center gap-2 text-body">
                  {crumbs.map((crumb, index) => {
                    const last = index === crumbs.length - 1;
                    return (
                      <Fragment
                        // The labels up to this crumb: two crumbs of the same name stay apart.
                        key={crumbs
                          .slice(0, index + 1)
                          .map((each) => each.label)
                          .join("/")}
                      >
                        {index > 0 && (
                          // The design's "›", a character: a right-to-left line draws it mirrored.
                          <li
                            aria-hidden="true"
                            className="text-muted-foreground"
                          >
                            ›
                          </li>
                        )}
                        <li
                          aria-current={last ? "page" : undefined}
                          className={cn(
                            "min-w-0",
                            last
                              ? "font-medium text-foreground"
                              : "text-muted-foreground",
                          )}
                        >
                          {crumb.href && !last ? (
                            <Truncate
                              render={
                                <Link
                                  href={crumb.href}
                                  className="block min-w-0 hover:text-foreground"
                                />
                              }
                            >
                              {crumb.label}
                            </Truncate>
                          ) : (
                            <Truncate>{crumb.label}</Truncate>
                          )}
                        </li>
                      </Fragment>
                    );
                  })}
                </ol>
              </nav>
            )}
            <main className="mx-auto w-full max-w-content-max flex-1 px-4 pt-4 pb-6 tablet:px-6 tablet:py-5">
              {children}
            </main>
          </div>
        </div>
      </div>

      <CommandDialog
        open={palette}
        onOpenChange={setPalette}
        title={t("commandTitle")}
      >
        <Command>
          <CommandInput
            placeholder={t("commandPlaceholder")}
            hint={t("commandClose")}
          />
          <CommandList>
            <CommandEmpty>{t("commandEmpty")}</CommandEmpty>
            {commands.map((section) => (
              <CommandGroup key={section.id} heading={section.label}>
                {section.items.map((entry) => (
                  <CommandItem
                    key={entry.id}
                    value={`${section.label} ${entry.label}`}
                    onSelect={() => {
                      setPalette(false);
                      if (entry.href) onNavigate(entry.href);
                      entry.onSelect?.();
                    }}
                  >
                    {entry.icon && <Icon icon={entry.icon} />}
                    {entry.label}
                  </CommandItem>
                ))}
              </CommandGroup>
            ))}
          </CommandList>
        </Command>
      </CommandDialog>
    </div>
  );
}

/**
 * One item of a menu. On the rail its label is the link's tooltip (the Screen sizes rules); in the expanded sidebar and the
 * drawer the label shows, and a label cut with "…" gets the same tooltip with its full text (the Long text pattern). The tooltip
 * opens from the link, so no control sits inside it.
 */
function MenuLink({
  entry,
  place,
  active,
  linkComponent: Link,
  onFollow,
}: {
  entry: NavItem;
  place: Place | "drawer";
  active: boolean;
  linkComponent: ElementType;
  onFollow?: () => void;
}) {
  const label = useRef<HTMLSpanElement>(null);
  const cut = useCut(label);
  const rail = place === "rail";
  const link = (
    <Link
      href={entry.href}
      aria-current={active ? "page" : undefined}
      onClick={onFollow}
      className={cn(
        rowBase,
        "text-muted-foreground",
        rail && railItem,
        place === "drawer" && drawerRow,
        active && "bg-accent font-medium text-foreground",
      )}
    >
      <Icon
        icon={entry.icon}
        size={16}
        className={cn(
          "shrink-0",
          rail && "max-desktop:size-4.5",
          place === "drawer" && "size-4.5",
        )}
      />
      <span
        ref={label}
        className={cn("min-w-0 flex-1 truncate", rail && "max-desktop:sr-only")}
      >
        {entry.label}
      </span>
      {entry.count !== undefined && (
        <span
          className={cn(
            "text-status tabular-nums text-muted-foreground",
            rail && "max-desktop:hidden",
          )}
        >
          {entry.count}
        </span>
      )}
    </Link>
  );
  if (!rail && !cut) return link;
  return (
    <Tooltip>
      <TooltipTrigger render={link} />
      <TooltipContent
        side="inline-end"
        // On the rail of a desktop the label shows: the tooltip stays only for a cut label.
        className={cn(rail && !cut && "desktop:hidden")}
      >
        {entry.label}
      </TooltipContent>
    </Tooltip>
  );
}
