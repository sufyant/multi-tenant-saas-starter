"use client";

import { useTranslations } from "next-intl";
import {
  type ElementType,
  type MouseEvent,
  type ReactNode,
  useLayoutEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import type { Instant } from "../../lib/format-date-time";
import type { Problem } from "../../lib/use-failure-text";
import { useOnline } from "../../lib/use-online";
import { cn } from "../../lib/utils";
import { Banner } from "../banner";
import { Button } from "../button";
import { EmptyState } from "../empty-state";
import { BellIcon, Icon, XIcon } from "../icon";
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

/** One notification as the bell shows it: the API's title and body, and the address that the app built from its link. */
export type NotificationEntry = {
  id: string;
  title: string;
  body: string;
  createdAt: string;
  readAt: string | null;
  href: string;
};

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
  /** The reader opened an item: mark it read and follow its link at once. */
  onOpenItem: (entry: NotificationEntry) => void;
  /** Marks read what this bell shows. */
  onMarkAll: () => void;
  markingAll: boolean;
  /** The reader's time zone, from GetMe; the browser's without one (F18). */
  timeZone?: string | null;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
  /** Tests and previews only: the "now" of the relative times. */
  now?: Instant;
};

/** Above 99 the badge says 99+ (the Notifications boards). */
const countText = (unread: number) => (unread > 99 ? "99+" : String(unread));
const highlightMs = 2000;

// A media query takes no variable, so the bell reads the breakpoint from its token at run time (F20). Without it, as in jsdom, the
// screen counts as wide.
const subscribeToWidth = (changed: () => void) => {
  window.addEventListener("resize", changed);
  return () => window.removeEventListener("resize", changed);
};
const isPhone = () => {
  const tablet = getComputedStyle(document.documentElement)
    .getPropertyValue("--bp-tablet")
    .trim();
  return tablet !== "" && !window.matchMedia(`(min-width: ${tablet})`).matches;
};

/**
 * The bell before the avatar and its list (the Notifications boards, `.st-bell` and `.st-notifs`): the unread count as a neutral
 * badge, a popover on a tablet and a desktop, a full screen on a phone. The list shows the API's title and body as they come, with
 * the relative time and an unread mark that is not colour alone. A new item never moves the list under a reader who has scrolled:
 * "1 new" waits at the top; at the top it comes in first, highlighted. Both say so once in a polite live region.
 */
export function NotificationBell(props: NotificationBellProps) {
  const t = useTranslations();
  const phone = useSyncExternalStore(subscribeToWidth, isPhone, () => false);
  const { unread, open, onOpenChange } = props;
  const label = unread
    ? t("notifications.bellUnread", { count: countText(unread) })
    : t("notifications.bell");
  const button = (
    <>
      <Icon icon={BellIcon} size={18} />
      {unread ? (
        <span
          aria-hidden="true"
          data-slot="bell-count"
          className="absolute end-0.5 top-1 min-w-4 rounded-full bg-neutral-text px-1 text-center text-[10px] leading-4 font-semibold text-background tabular-nums ring-2 ring-background"
        >
          {countText(unread)}
        </span>
      ) : null}
    </>
  );
  const buttonClass =
    "relative inline-flex size-control-lg shrink-0 items-center justify-center rounded-md text-muted-foreground outline-none hover:bg-accent hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/50 aria-expanded:bg-accent aria-expanded:text-foreground";

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
            className="h-dvh gap-0 bg-background p-0 text-foreground"
          >
            <NotificationPanel
              {...props}
              title={
                <SheetTitle className="text-body">
                  {t("notifications.title")}
                </SheetTitle>
              }
              close={
                <Button
                  variant="ghost"
                  size="lg"
                  aria-label={t("common.close")}
                  icon={<Icon icon={XIcon} />}
                  onClick={() => onOpenChange(false)}
                />
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
        align="end"
        sideOffset={6}
        className="max-h-[560px] w-[400px] max-w-[calc(100vw-2rem)] gap-0 overflow-hidden p-0"
      >
        <NotificationPanel
          {...props}
          title={
            <PopoverTitle className="text-body">
              {t("notifications.title")}
            </PopoverTitle>
          }
        />
      </PopoverContent>
    </Popover>
  );
}

/** The list's frame: the header, the offline banner, the items or their state, and the foot. */
function NotificationPanel({
  unread,
  list,
  onOpenItem,
  onMarkAll,
  markingAll,
  timeZone,
  linkComponent: Link = "a",
  now,
  title,
  close,
}: NotificationBellProps & { title: ReactNode; close?: ReactNode }) {
  const t = useTranslations("notifications");
  const online = useOnline();
  const scroll = useRef<HTMLDivElement>(null);
  const items = list.status === "success" ? list.items : undefined;
  const arrivals = useArrivals(items, scroll);
  const shown = items?.filter((entry) => !arrivals.held.includes(entry.id));

  return (
    <div className="flex max-h-[inherit] min-h-0 flex-1 flex-col">
      <div className="flex min-h-12 shrink-0 items-center gap-2 border-b border-border ps-4 pe-2">
        {close}
        <div className="min-w-0 flex-1">{title}</div>
        {unread ? (
          <Button
            variant="ghost"
            disabled={!online || markingAll}
            onClick={onMarkAll}
          >
            {t("markAll")}
          </Button>
        ) : null}
      </div>
      {!online && (
        <div className="mx-3 mt-3 mb-1">
          <Banner tone="warning" title={t("offlineTitle")}>
            {t("offlineText")}
          </Banner>
        </div>
      )}
      <div
        ref={scroll}
        data-slot="notification-scroll"
        className="relative min-h-0 flex-1 overflow-y-auto overscroll-contain"
      >
        {arrivals.held.length > 0 && (
          <div className="sticky top-2 z-10 flex h-0 justify-center overflow-visible">
            <Button className="shadow-md" onClick={arrivals.release}>
              {t("newCount", { count: arrivals.held.length })}
            </Button>
          </div>
        )}
        {list.status === "loading" && (
          <div className="px-4 pt-2 pb-4">
            <Skeleton label={t("loading")} />
          </div>
        )}
        {list.status === "error" && (
          <div className="px-4 pt-2 pb-4">
            <ListError
              title={t("errorTitle")}
              problem={list.problem}
              at={list.at}
              onRetry={list.onRetry}
            />
          </div>
        )}
        {shown?.length === 0 && (
          <div className="px-4 pt-2 pb-4">
            <EmptyState title={t("emptyTitle")} description={t("emptyText")} />
          </div>
        )}
        {shown && shown.length > 0 && (
          <ul aria-label={t("title")}>
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
      </div>
      {list.status === "success" && list.items.length > 0 && (
        <div className="shrink-0 border-t border-border px-4 py-3 text-center">
          {list.hasMore ? (
            <Button
              className="w-full"
              disabled={list.loadingMore}
              onClick={list.onLoadMore}
            >
              {t("more")}
            </Button>
          ) : (
            <p className="text-small text-muted-foreground">{t("end")}</p>
          )}
        </div>
      )}
    </div>
  );
}

/** A click that opens the link in this tab. A modified click opens a new tab, as a link does, and still marks the item read. */
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
  onOpen: (entry: NotificationEntry) => void;
  timeZone?: string | null;
  linkComponent: ElementType;
  now?: Instant;
}) {
  const t = useTranslations();
  const unread = entry.readAt === null;
  return (
    <li
      data-fresh={fresh || undefined}
      className={cn(
        "relative grid grid-cols-[7px_minmax(0,1fr)] gap-x-2.5 border-t border-border px-4 py-3 transition-colors first:border-t-0 hover:bg-accent motion-reduce:transition-none",
        fresh &&
          "bg-accent motion-safe:animate-in motion-safe:slide-in-from-top-2",
      )}
    >
      <span
        aria-hidden="true"
        className={cn("mt-[7px] size-[7px] rounded-full", unread && "bg-info")}
      />
      <div className="flex min-w-0 flex-col gap-0.5">
        <Link
          href={entry.href}
          onClick={(event: MouseEvent) => {
            if (plainClick(event)) event.preventDefault();
            onOpen(entry);
          }}
          className={cn(
            "text-body [overflow-wrap:anywhere] text-foreground outline-none after:absolute after:inset-0 focus-visible:after:rounded-md focus-visible:after:outline-2 focus-visible:after:-outline-offset-2 focus-visible:after:outline-ring",
            unread && "font-semibold",
          )}
        >
          {unread && (
            <span className="sr-only">{t("notifications.unread")} </span>
          )}
          {entry.title}
        </Link>
        <p className="text-small [overflow-wrap:anywhere] text-muted-foreground">
          {entry.body}
        </p>
        <Time
          at={entry.createdAt}
          show="relative"
          now={now}
          locale={t("locale.format")}
          timeZone={timeZone ?? undefined}
          className="relative z-10 mt-0.5 self-start text-small text-muted-foreground"
        />
      </div>
    </li>
  );
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
