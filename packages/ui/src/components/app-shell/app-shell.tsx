"use client";

import { useTranslations } from "next-intl";
import {
  type ElementType,
  Fragment,
  type ReactNode,
  useEffect,
  useState,
  useSyncExternalStore,
} from "react";
import type { Environment } from "../../lib/environment";
import { usePhone } from "../../lib/use-phone";
import { useTabTitle } from "../../lib/use-tab-title";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import { EnvironmentStrip } from "../environment-strip";
import {
  ArrowLeftIcon,
  ChevronRightIcon,
  Icon,
  type LucideIcon,
  MenuIcon,
  SearchIcon,
} from "../icon";
import { Truncate } from "../truncate";
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
  environment: Environment;
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
  "group/row flex h-control-md items-center gap-2.5 rounded-md px-2 text-body outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring max-tablet:h-touch-target";

/**
 * The frame of every signed-in page (the Shell screens and the Screen sizes rules). From bp-desktop the sidebar is open with its
 * labels: the head, search (⌘K), Notifications, the main menu, then Settings and the account at its foot; inside Settings the
 * settings layout takes its place. On a tablet it is an icon rail, each label a tooltip. Under bp-tablet a top bar holds the menu
 * button, the page's name and the bell, and the menu opens as a drawer. The content sits in a card with the breadcrumb above it.
 * Outside production the environment strip sits above everything. Only ⌘K or Ctrl K is a shortcut. The whole shell mirrors in
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
  environment,
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
  const strip = environment.kind !== "production";

  /** One item of a menu. On the rail its label is a tooltip; in the drawer and the expanded sidebar it shows. */
  const item = (entry: NavItem, place: Place | "drawer") => {
    const active = entry.id === current;
    const link = (
      <Link
        href={entry.href}
        aria-current={active ? "page" : undefined}
        onClick={place === "drawer" ? () => setDrawer(false) : undefined}
        className={cn(
          rowBase,
          "text-muted-foreground",
          place === "rail" &&
            "max-desktop:size-10 max-desktop:justify-center max-desktop:px-0",
          active && "bg-accent font-medium text-foreground",
        )}
      >
        <Icon icon={entry.icon} size={16} className="shrink-0" />
        <span
          className={cn(
            "min-w-0 flex-1",
            place === "rail" && "max-desktop:sr-only",
          )}
        >
          <Truncate>{entry.label}</Truncate>
        </span>
        {entry.count !== undefined && (
          <span
            className={cn(
              "text-small font-medium tabular-nums text-muted-foreground",
              place === "rail" && "max-desktop:hidden",
            )}
          >
            {entry.count}
          </span>
        )}
      </Link>
    );
    if (place !== "rail") return <Fragment key={entry.id}>{link}</Fragment>;
    return (
      <Tooltip key={entry.id}>
        <TooltipTrigger render={link} />
        <TooltipContent side="inline-end" className="desktop:hidden">
          {entry.label}
        </TooltipContent>
      </Tooltip>
    );
  };

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
          "flex h-control-sm items-center gap-2 rounded-md border border-border bg-card px-2 text-small text-muted-foreground shadow-xs outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring max-tablet:h-touch-target",
          place === "rail" &&
            "max-desktop:size-10 max-desktop:justify-center max-desktop:border-0 max-desktop:bg-transparent max-desktop:px-0 max-desktop:shadow-none",
        )}
      >
        <Icon icon={SearchIcon} size={14} className="shrink-0" />
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
            <div key={group.id} className="mt-2 flex flex-col gap-0.5">
              <span
                className={cn(
                  "px-2 pt-2 pb-1 text-small font-medium text-muted-foreground",
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
        <div className="mt-2 flex flex-col gap-0.5">
          {search(place)}
          {place !== "drawer" && bell?.(wide)}
        </div>
        <div className="mt-2 flex flex-col gap-0.5">
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
    <div
      className={cn(
        "flex min-h-dvh flex-col bg-sidebar text-foreground",
        strip
          ? "[--strip-height:calc(28px+env(safe-area-inset-top))] tablet:[--strip-height:24px]"
          : "[--strip-height:0px]",
      )}
    >
      <EnvironmentStrip environment={environment} />
      <div className="flex flex-1">
        {!phone && (
          <nav
            aria-label={
              settingsMenu ? t("settingsNavigation") : t("navigation")
            }
            className="sticky top-(--strip-height) hidden h-[calc(100dvh-var(--strip-height))] w-rail-width shrink-0 flex-col gap-0.5 overflow-y-auto px-2 py-3 tablet:flex desktop:w-rail-expanded desktop:px-2.5"
          >
            {menu("rail")}
          </nav>
        )}

        {phone && (
          <Sheet open={drawer} onOpenChange={setDrawer}>
            <SheetContent
              side="inline-start"
              className="w-72 gap-0 bg-sidebar px-2.5 pt-[max(var(--space-3),env(safe-area-inset-top))] pb-[max(var(--space-3),env(safe-area-inset-bottom))]"
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
                <p className="px-2 pt-2 text-small text-muted-foreground">
                  {t("version", { version })}
                </p>
              )}
            </SheetContent>
          </Sheet>
        )}

        <div className="flex min-w-0 flex-1 flex-col tablet:py-2 tablet:pe-2">
          <div className="flex min-w-0 flex-1 flex-col bg-background tablet:overflow-hidden tablet:rounded-lg tablet:border tablet:border-border tablet:shadow-xs">
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
                className="flex h-12 shrink-0 items-center border-b border-border px-6"
              >
                <ol className="flex min-w-0 items-center gap-1.5 text-body">
                  {crumbs.map((crumb, index) => {
                    const last = index === crumbs.length - 1;
                    return (
                      <Fragment key={crumb.href ?? crumb.label}>
                        {index > 0 && (
                          <li
                            aria-hidden="true"
                            className="text-muted-foreground"
                          >
                            <Icon
                              icon={ChevronRightIcon}
                              size={14}
                              directional
                            />
                          </li>
                        )}
                        <li
                          aria-current={last ? "page" : undefined}
                          className={cn(
                            "min-w-0",
                            last ? "text-foreground" : "text-muted-foreground",
                          )}
                        >
                          {crumb.href && !last ? (
                            <Link
                              href={crumb.href}
                              className="hover:text-foreground"
                            >
                              <Truncate>{crumb.label}</Truncate>
                            </Link>
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
            <main className="mx-auto w-full max-w-content-max flex-1 px-4 py-6 tablet:px-6">
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
          <CommandInput placeholder={t("commandPlaceholder")} />
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
