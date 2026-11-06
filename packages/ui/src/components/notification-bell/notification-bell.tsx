"use client";

import { useTranslations } from "next-intl";
import {
  type ElementType,
  type MouseEvent,
  type ReactNode,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import type { Instant } from "../../lib/format-date-time";
import type { Problem } from "../../lib/use-failure-text";
import { useOnline } from "../../lib/use-online";
import { usePhone } from "../../lib/use-phone";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import { EmptyState } from "../empty-state";
import { BellIcon, CircleXIcon, Icon, WifiOffIcon } from "../icon";
import { ListError } from "../list-error";
import { Skeleton } from "../skeleton";
import { Time } from "../time";
import {
  Popover,
  PopoverContent,
  PopoverTitle,
  PopoverTrigger,
} from "../ui/popover";
import { Sheet, SheetContent, SheetTitle } from "../ui/sheet";
import {
  TabsContent,
  TabsList,
  Tabs as TabsRoot,
  TabsTrigger,
} from "../ui/tabs";

/**
 * One notification as the bell shows it: the API's title, the address that the app built from its link, and the words of its
 * meta line, all from the API: the organisation it is about (the tenant web), its area, and the person who acted (the platform).
 * The body stays on the notification's page and in its email (F57).
 */
export type NotificationEntry = {
  id: string;
  title: string;
  createdAt: string;
  readAt: string | null;
  href: string;
  organisation?: string;
  area: string;
  actor?: string;
  /** Read while the Unread tab was open: the list no longer holds it, so the bell keeps it in its place, drawn as read (F57). */
  readHere?: true;
};

/** The bell's two tabs: the unread items only, or every item (3.01, 3.02). */
export type NotificationTab = "unread" | "all";

/** The list's read: waiting, failed, or its pages so far. */
export type NotificationListState =
  | { status: "loading" }
  | { status: "error"; problem: Problem; at: number; onRetry: () => void }
  | {
      status: "success";
      items: NotificationEntry[];
      hasMore: boolean;
      loadingMore: boolean;
      onLoadMore: () => void;
    };

export type NotificationBellProps = {
  /** The bell's unread count; none until it is read. */
  unread?: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  list: NotificationListState;
  /**
   * The reader opened an item: mark it read, and with `follow` close the bell and follow its link at once. A modified click opens
   * the link in a new tab, as a link does, so `follow` is false and this tab stays.
   */
  onOpenItem: (entry: NotificationEntry, open: { follow: boolean }) => void;
  /** Marks read what this bell shows. */
  onMarkAll: () => void;
  markingAll: boolean;
  /** The tab the list shows; the app reads the list for it. */
  tab: NotificationTab;
  onTabChange: (tab: NotificationTab) => void;
  /** The reader's time zone, from GetMe; the browser's without one (F18). */
  timeZone?: string | null;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /** Tests and previews only: the "now" of the relative times. */
  now?: Instant;
  /**
   * Where the bell sits (the Shell screens): a row, "Notifications" and the count; the sidebar's place, an icon on a tablet's rail
   * and the row from bp-desktop; or an icon of the phone's top bar, the default.
   */
  place?: "row" | "rail" | "icon";
  /** The bell's own page: on a phone the bell links to it instead of opening its full screen (3.08 of the platform). */
  pageHref?: string;
};

/** The page of notifications: the bell's list without the bell. */
export type NotificationPageProps = Omit<
  NotificationBellProps,
  "open" | "onOpenChange" | "place" | "pageHref"
>;

/** Above 99 the badge says 99+ (the Notifications boards). */
const countText = (unread: number) => (unread > 99 ? "99+" : String(unread));
const highlightMs = 2000;

/**
 * The bell before the avatar and its list (the Notifications boards, `.st-bell` and `.st-notifs`): the unread count as a neutral
 * badge, a popover on a tablet and a desktop, a full screen on a phone. The list shows the API's title as it comes, one line of the
 * organisation and the relative time, and an unread mark that is not colour alone. A new item never moves the list under a reader who has scrolled:
 * "1 new" waits at the top; at the top it comes in first, highlighted. Both say so once in a polite live region.
 */
export function NotificationBell(props: NotificationBellProps) {
  const t = useTranslations();
  const phone = usePhone();
  const {
    unread,
    open,
    onOpenChange,
    place = "icon",
    pageHref,
    linkComponent: Link = "a",
  } = props;
  const label = unread
    ? t("notifications.bellUnread", { count: countText(unread) })
    : t("notifications.bell");
  // A sidebar row: "Notifications" and the count. In the sidebar's place (`rail`) a tablet's icon rail shows the bell alone and
  // the count on its corner.
  const row = place === "row" || place === "rail";
  const rail = place === "rail";
  const count = unread ? (
    <span
      aria-hidden="true"
      data-slot="bell-count"
      className={cn(
        "rounded-full bg-primary px-1.5 text-status text-primary-foreground tabular-nums",
        row
          ? rail &&
              "max-desktop:absolute max-desktop:start-6 max-desktop:top-1.5"
          : "absolute start-6 top-1.5",
      )}
    >
      {countText(unread)}
    </span>
  ) : null;
  const button = row ? (
    <>
      <Icon
        icon={BellIcon}
        size={16}
        className={cn("shrink-0", rail && "max-desktop:size-4.5")}
      />
      <span
        aria-hidden="true"
        className={cn(
          "min-w-0 flex-1 truncate text-start",
          rail && "max-desktop:sr-only",
        )}
      >
        {t("notifications.bell")}
      </span>
      {/* The flex row draws no space; the space keeps "Notifications 2" one phrase of the name (WCAG 2.5.3). */}
      {count && " "}
      {count}
    </>
  ) : (
    <>
      <Icon icon={BellIcon} size={20} />
      {count}
    </>
  );
  const buttonClass = cn(
    "relative inline-flex shrink-0 items-center rounded-md text-muted-foreground outline-none hover:bg-accent hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/50 aria-expanded:bg-accent aria-expanded:text-foreground",
    row
      ? cn(
          "h-7.5 w-full gap-2.5 px-2 text-body",
          rail &&
            "max-desktop:size-12 max-desktop:justify-center max-desktop:px-0",
        )
      : "size-touch-target justify-center",
  );

  if (phone && pageHref)
    return (
      <Link href={pageHref} aria-label={label} className={buttonClass}>
        {button}
      </Link>
    );

  if (phone)
    return (
      <>
        <button
          type="button"
          aria-label={label}
          aria-expanded={open}
          className={buttonClass}
          onClick={() => onOpenChange(true)}
        >
          {button}
        </button>
        <Sheet open={open} onOpenChange={(next) => onOpenChange(next)}>
          <SheetContent
            side="bottom"
            // The sheet's bottom side sets its own height; the bell's full screen takes the whole screen (3.14).
            className="gap-0 bg-background p-0 text-foreground data-[side=bottom]:h-dvh"
          >
            <NotificationPanel
              {...props}
              layout="screen"
              title={
                <SheetTitle className="text-section-title">
                  {t("notifications.title")}
                </SheetTitle>
              }
              close={
                <Button variant="ghost" onClick={() => onOpenChange(false)}>
                  {t("common.close")}
                </Button>
              }
            />
          </SheetContent>
        </Sheet>
      </>
    );

  return (
    <Popover open={open} onOpenChange={(next) => onOpenChange(next)}>
      <PopoverTrigger
        render={<button type="button" aria-label={label} />}
        className={buttonClass}
      >
        {button}
      </PopoverTrigger>
      <PopoverContent
        side={row ? "inline-end" : "bottom"}
        align={row ? "start" : "end"}
        sideOffset={6}
        className="max-h-[560px] w-95 max-w-[calc(100vw-2rem)] gap-0 overflow-hidden rounded-[10px] p-1"
      >
        <NotificationPanel
          {...props}
          title={
            <PopoverTitle className="text-section-title">
              {t("notifications.title")}
            </PopoverTitle>
          }
        />
      </PopoverContent>
    </Popover>
  );
}

/**
 * The platform's page of notifications on a phone (3.08 of the platform): under the top bar, which names the page, the tabs, a
 * full-width Mark all as read and the items in a card.
 */
export function NotificationPage(props: NotificationPageProps) {
  return <NotificationPanel {...props} layout="page" />;
}

/**
 * The list's frame (3.01 to 3.08, 3.14, 3.15): the header with Mark all as read, the tabs, the offline row, the items or their
 * state, and the foot. A popover keeps Mark all in its header, disabled while it has nothing to do; the phone's full screen has
 * Close in its header and Mark all on the tabs' row; the platform's page is the full screen without its header (3.08 is 3.14).
 */
function NotificationPanel({
  unread,
  list,
  onOpenItem,
  onMarkAll,
  markingAll,
  tab,
  onTabChange,
  timeZone,
  linkComponent: Link = "a",
  now,
  title,
  close,
  layout = "popover",
}: NotificationPageProps & {
  title?: ReactNode;
  close?: ReactNode;
  layout?: "popover" | "screen" | "page";
}) {
  const t = useTranslations("notifications");
  const online = useOnline();
  const scroll = useRef<HTMLDivElement>(null);
  const loaded = list.status === "success" ? list.items : undefined;
  const items = useReadHere(loaded, tab);
  const arrivals = useArrivals(items, scroll);
  const shown = items?.filter((entry) => !arrivals.held.includes(entry.id));
  const phone = layout !== "popover";
  const markAll = (
    <Button
      variant="ghost"
      disabled={!online || markingAll || !unread || !items}
      onClick={onMarkAll}
    >
      {t("markAll")}
    </Button>
  );
  const tabs = (
    <TabsList
      variant="line"
      aria-label={t("tabs")}
      className="h-auto gap-4 p-0"
    >
      {(["unread", "all"] as const).map((id) => (
        <TabsTrigger
          key={id}
          value={id}
          className="h-auto flex-none rounded-none border-0 p-0 text-small font-normal text-muted-foreground after:hidden data-active:font-medium data-active:text-foreground"
        >
          {id === "unread" && unread
            ? t("tabUnreadCount", { count: countText(unread) })
            : t(id === "unread" ? "tabUnread" : "tabAll")}
        </TabsTrigger>
      ))}
    </TabsList>
  );

  return (
    <TabsRoot
      value={tab}
      onValueChange={(value) => onTabChange(value as NotificationTab)}
      className={cn(
        "flex max-h-[inherit] min-h-0 flex-1 flex-col gap-0",
        layout === "page" && "gap-3",
      )}
    >
      {layout === "popover" && (
        <div className="flex shrink-0 flex-col border-b border-border">
          <div className="flex items-center justify-between gap-2 ps-2.5 pt-1">
            <div className="min-w-0 flex-1">{title}</div>
            {markAll}
          </div>
          <div className="ps-2.5 pb-1.5">{tabs}</div>
        </div>
      )}
      {layout === "screen" && (
        <>
          <div className="flex shrink-0 items-center justify-between gap-2 border-b border-border px-4 py-2">
            <div className="min-w-0 flex-1">{title}</div>
            {close}
          </div>
          <div className="flex h-11 shrink-0 items-center justify-between gap-2 ps-4 pe-2">
            {tabs}
            {items && items.length > 0 && markAll}
          </div>
        </>
      )}
      {layout === "page" && (
        <div className="flex h-11 shrink-0 items-center justify-between gap-2">
          {tabs}
          {items && items.length > 0 && markAll}
        </div>
      )}
      <TabsContent
        value={tab}
        ref={scroll}
        data-slot="notification-scroll"
        className={cn(
          "relative flex min-h-0 flex-1 flex-col gap-0.5 overflow-y-auto overscroll-contain",
          layout === "popover" && "py-1",
          layout === "screen" && "px-2",
          layout === "page" && "flex-none overflow-visible",
        )}
      >
        {!online && (
          <div className="flex shrink-0 gap-2 rounded-md bg-muted px-2.5 py-2 text-small">
            <Icon icon={WifiOffIcon} size={16} className="mt-px shrink-0" />
            <div className="min-w-0">
              <p className="font-medium">{t("offlineTitle")}</p>
              <p className="text-muted-foreground">{t("offlineText")}</p>
            </div>
          </div>
        )}
        {arrivals.held.length > 0 && (
          <div className="sticky top-2 z-10 flex h-0 justify-center overflow-visible">
            <Button className="shadow-md" onClick={arrivals.release}>
              {t("newCount", { count: arrivals.held.length })}
            </Button>
          </div>
        )}
        {list.status === "loading" && (
          <Skeleton rows={4} shape="lines" label={t("loading")} />
        )}
        {list.status === "error" && (
          <ListError
            size="compact"
            icon={CircleXIcon}
            title={t("errorTitle")}
            problem={list.problem}
            at={list.at}
            onRetry={list.onRetry}
          />
        )}
        {shown?.length === 0 && (
          <EmptyState
            size="compact"
            icon={BellIcon}
            title={t("emptyTitle")}
            description={t("emptyText")}
          />
        )}
        {shown && shown.length > 0 && (
          <ul aria-label={t("title")} className="flex flex-col gap-0.5">
            {shown.map((entry) => (
              <Item
                key={entry.id}
                entry={entry}
                fresh={arrivals.fresh.includes(entry.id)}
                onOpen={onOpenItem}
                timeZone={timeZone}
                linkComponent={Link}
                now={now}
              />
            ))}
          </ul>
        )}
        <p role="status" aria-live="polite" className="sr-only">
          {arrivals.announced}
        </p>
      </TabsContent>
      {!phone || (items && items.length > 0) ? (
        <div
          className={cn(
            "flex min-h-5.5 shrink-0 items-center ps-2.5 pe-1 pt-1",
            layout !== "page" && "border-t border-border",
          )}
        >
          {items && items.length > 0 && list.status === "success" ? (
            list.hasMore ? (
              <Button
                variant="ghost"
                className="-ms-3"
                disabled={list.loadingMore}
                onClick={list.onLoadMore}
              >
                {t("more")}
              </Button>
            ) : (
              <p className="text-small text-muted-foreground">{t("end")}</p>
            )
          ) : null}
        </div>
      ) : null}
    </TabsRoot>
  );
}

/** A click that opens the link in this tab. A modified click opens a new tab, as a link does. */
const plainClick = (event: MouseEvent) =>
  event.button === 0 &&
  !event.metaKey &&
  !event.ctrlKey &&
  !event.shiftKey &&
  !event.altKey;

function Item({
  entry,
  fresh,
  onOpen,
  timeZone,
  linkComponent: Link,
  now,
}: {
  entry: NotificationEntry;
  fresh: boolean;
  onOpen: NotificationBellProps["onOpenItem"];
  timeZone?: string | null;
  linkComponent: ElementType;
  now?: Instant;
}) {
  const t = useTranslations();
  const unread = entry.readAt === null && !entry.readHere;
  return (
    <li
      data-fresh={fresh || undefined}
      className={cn(
        "relative grid grid-cols-[8px_minmax(0,1fr)] gap-x-2.5 rounded-md px-3 pt-2.5 pb-2 transition-colors hover:bg-accent motion-reduce:transition-none",
        unread && "bg-primary-subtle",
        fresh &&
          "bg-accent motion-safe:animate-in motion-safe:slide-in-from-top-2",
      )}
    >
      <span
        aria-hidden="true"
        className={cn("mt-[5px] size-2 rounded-full", unread && "bg-primary")}
      />
      {/* 4 px between the title's link and the time's button keep WCAG 2.5.8's spacing for both; the row stays 56 px (3.01). */}
      <div className="flex min-w-0 flex-col gap-1">
        <Link
          href={entry.href}
          onClick={(event: MouseEvent) => {
            const follow = plainClick(event);
            if (follow) event.preventDefault();
            onOpen(entry, { follow });
          }}
          className={cn(
            "text-small [overflow-wrap:anywhere] text-foreground outline-none after:absolute after:inset-0 focus-visible:after:rounded-md focus-visible:after:outline-2 focus-visible:after:-outline-offset-2 focus-visible:after:outline-ring",
            unread && "font-medium",
          )}
        >
          {unread && (
            <span className="sr-only">{t("notifications.unread")} </span>
          )}
          {entry.title}
        </Link>
        {/* One line: the organisation, the area, the person who acted and the time, as the API gives them (3.01). The time sits
            above the link's hit area, so its tooltip opens (F18). */}
        <p className="text-status [overflow-wrap:anywhere] text-muted-foreground">
          {(
            [
              ["organisation", entry.organisation],
              ["area", entry.area],
              ["actor", entry.actor],
            ] as const
          )
            .filter(([, part]) => part)
            .map(([key, part]) => (
              <span key={key}>
                <bdi>{part}</bdi>
                {" · "}
              </span>
            ))}
          <Time
            at={entry.createdAt}
            show="relative"
            now={now}
            locale={t("locale.format")}
            timeZone={timeZone ?? undefined}
            className="relative z-10"
          />
        </p>
      </div>
    </li>
  );
}

/** The Unread tab's items with those read while it was open, each back in its place (F57). */
function useReadHere(
  items: NotificationEntry[] | undefined,
  tab: NotificationTab,
) {
  // The shown list derives from the last one shown ("Storing information from previous renders", React documentation).
  const [last, setLast] = useState({ items, tab, shown: items });
  if (last.items === items && last.tab === tab) return last.shown;
  const shown =
    items && tab === "unread" && last.tab === tab
      ? withReadHere(items, last.shown ?? [])
      : items;
  setLast({ items, tab, shown });
  return shown;
}

/** The new list, with each item of the shown one that left it put back after the item it followed, drawn as read. */
function withReadHere(items: NotificationEntry[], shown: NotificationEntry[]) {
  const ids = new Set(items.map((item) => item.id));
  const merged = [...items];
  let at = 0;
  for (const entry of shown) {
    if (ids.has(entry.id))
      at = merged.findIndex((item) => item.id === entry.id) + 1;
    else merged.splice(at++, 0, { ...entry, readHere: true });
  }
  return merged;
}

/**
 * What came in since the list was last shown: the items new at its top. A reader who has scrolled keeps their place, so the new
 * items wait (`held`) until they press "1 new"; at the top they come in at once, highlighted for two seconds (`fresh`). An older
 * page adds items at the end, which are not new.
 */
function useArrivals(
  items: NotificationEntry[] | undefined,
  scroll: React.RefObject<HTMLDivElement | null>,
) {
  const t = useTranslations("notifications");
  const known = useRef<Set<string> | null>(null);
  const [held, setHeld] = useState<string[]>([]);
  const [fresh, setFresh] = useState<string[]>([]);
  const [announced, setAnnounced] = useState("");

  // Before the browser paints, so a held item never shows for a frame.
  useLayoutEffect(() => {
    if (!items) return;
    const seen = known.current;
    known.current = new Set(items.map((entry) => entry.id));
    if (!seen) return;
    const firstSeen = items.findIndex((entry) => seen.has(entry.id));
    const top = firstSeen === -1 ? items : items.slice(0, firstSeen);
    const arrived = top.map((entry) => entry.id).filter((id) => !seen.has(id));
    if (arrived.length === 0) return;
    setAnnounced(t("announce", { count: arrived.length }));
    if ((scroll.current?.scrollTop ?? 0) > 0) {
      setHeld((before) => [...arrived, ...before]);
      return;
    }
    setFresh(arrived);
  }, [items, scroll, t]);

  useLayoutEffect(() => {
    if (fresh.length === 0) return;
    const timer = window.setTimeout(() => setFresh([]), highlightMs);
    return () => window.clearTimeout(timer);
  }, [fresh]);

  return {
    held,
    fresh,
    announced,
    release: () => {
      setHeld([]);
      if (scroll.current) scroll.current.scrollTop = 0;
    },
  };
}
