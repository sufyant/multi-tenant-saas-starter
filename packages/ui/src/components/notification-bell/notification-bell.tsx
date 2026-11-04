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
import { formatUtc, type Instant } from "../../lib/format-date-time";
import type { Problem } from "../../lib/use-failure-text";
import { useOnline } from "../../lib/use-online";
import { usePhone } from "../../lib/use-phone";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import {
  BellIcon,
  CircleXIcon,
  Icon,
  type LucideIcon,
  WifiOffIcon,
} from "../icon";
import { Time } from "../time";
import {
  Popover,
  PopoverContent,
  PopoverTitle,
  PopoverTrigger,
} from "../ui/popover";
import { Sheet, SheetContent, SheetTitle } from "../ui/sheet";
import { Skeleton as SkeletonBlock } from "../ui/skeleton";

/**
 * One notification as the bell shows it: the API's title, the address that the app built from its link, and the name of the
 * organisation it is about when the app knows it. The body stays on the notification's page and in its email (F57).
 */
export type NotificationEntry = {
  id: string;
  title: string;
  createdAt: string;
  readAt: string | null;
  href: string;
  organisation?: string;
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
  /**
   * The reader opened an item: mark it read, and with `follow` close the bell and follow its link at once. A modified click opens
   * the link in a new tab, as a link does, so `follow` is false and this tab stays.
   */
  onOpenItem: (entry: NotificationEntry, open: { follow: boolean }) => void;
  /** Marks read what this bell shows. */
  onMarkAll: () => void;
  markingAll: boolean;
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
};

/** Above 99 the badge says 99+ (the Notifications boards). */
const countText = (unread: number) => (unread > 99 ? "99+" : String(unread));
const highlightMs = 2000;

/**
 * The bell before the avatar and its list (the Notifications boards, `.st-bell` and `.st-notifs`): the unread count as a neutral
 * badge, a popover on a tablet and a desktop, a full screen on a phone. The list shows the API's title and body as they come, with
 * the relative time and an unread mark that is not colour alone. A new item never moves the list under a reader who has scrolled:
 * "1 new" waits at the top; at the top it comes in first, highlighted. Both say so once in a polite live region.
 */
export function NotificationBell(props: NotificationBellProps) {
  const t = useTranslations();
  const phone = usePhone();
  const { unread, open, onOpenChange, place = "icon" } = props;
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
              phone
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
 * The list's frame (3.01 to 3.08, 3.14, 3.15): the header with Mark all as read, the offline row, the items or their state, and
 * the foot. A popover keeps Mark all in its header, disabled while it has nothing to do; the phone's full screen has Close in its
 * header and Mark all on a row of its own above the items.
 */
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
  phone = false,
}: NotificationBellProps & {
  title: ReactNode;
  close?: ReactNode;
  phone?: boolean;
}) {
  const t = useTranslations("notifications");
  const tCommon = useTranslations("common");
  const online = useOnline();
  const scroll = useRef<HTMLDivElement>(null);
  const items = list.status === "success" ? list.items : undefined;
  const arrivals = useArrivals(items, scroll);
  const shown = items?.filter((entry) => !arrivals.held.includes(entry.id));
  const markAll = (
    <Button
      variant="ghost"
      disabled={!online || markingAll || !unread || !items}
      onClick={onMarkAll}
    >
      {t("markAll")}
    </Button>
  );

  return (
    <div className="flex max-h-[inherit] min-h-0 flex-1 flex-col">
      <div
        className={cn(
          "flex shrink-0 items-center justify-between gap-2 border-b border-border",
          phone ? "px-4 py-2" : "ps-2.5 pt-1 pb-1.5",
        )}
      >
        <div className="min-w-0 flex-1">{title}</div>
        {phone ? close : markAll}
      </div>
      {phone && items && items.length > 0 && (
        <div className="flex h-11 shrink-0 items-center justify-end ps-4 pe-2">
          {markAll}
        </div>
      )}
      <div
        ref={scroll}
        data-slot="notification-scroll"
        className={cn(
          "relative flex min-h-0 flex-1 flex-col gap-0.5 overflow-y-auto overscroll-contain",
          phone ? "px-2" : "py-1",
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
          <div role="status" aria-label={t("loading")} aria-busy="true">
            {[0, 1, 2, 3].map((row) => (
              <div key={row} className="flex flex-col gap-1.5 ps-7.5 py-2.5">
                <SkeletonBlock className="h-2.5 w-65 max-w-full rounded-sm motion-reduce:animate-none" />
                <SkeletonBlock className="h-2 w-40 max-w-full rounded-sm motion-reduce:animate-none" />
              </div>
            ))}
          </div>
        )}
        {list.status === "error" && (
          <PanelState
            icon={CircleXIcon}
            title={t("errorTitle")}
            role="alert"
            phone={phone}
          >
            {list.problem && (
              <ErrorReference problem={list.problem} at={list.at} />
            )}
            <Button onClick={list.onRetry}>{tCommon("tryAgain")}</Button>
          </PanelState>
        )}
        {shown?.length === 0 && (
          <PanelState icon={BellIcon} title={t("emptyTitle")} phone={phone}>
            <p className="text-small text-muted-foreground">{t("emptyText")}</p>
          </PanelState>
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
      </div>
      {!phone || (items && items.length > 0) ? (
        <div className="flex min-h-5.5 shrink-0 items-center border-t border-border ps-2.5 pe-1 pt-1">
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
    </div>
  );
}

/** The list's empty and error states (3.03, 3.05, 3.15): a 20 px icon, a short title and what to do; on a phone lower and smaller. */
function PanelState({
  icon,
  title,
  role,
  phone,
  children,
}: {
  icon: LucideIcon;
  title: string;
  role?: "alert";
  phone: boolean;
  children: ReactNode;
}) {
  return (
    <div
      role={role}
      className={cn(
        "flex flex-col items-center gap-1.5 px-4 text-center",
        phone ? "pt-40" : "py-7",
      )}
    >
      <Icon icon={icon} size={phone ? 16 : 20} />
      <p className="text-small font-medium">{title}</p>
      {children}
    </div>
  );
}

/** The reference that helps support find the failed read, "Ref 4c19ab · 13:32 UTC" (the ListStates board). */
function ErrorReference({
  problem,
  at,
}: {
  problem: NonNullable<Problem>;
  at: number;
}) {
  const t = useTranslations();
  return (
    <p dir="ltr" className="font-mono text-mono text-muted-foreground">
      {t("common.reference", {
        id: problem.reference,
        time: formatUtc(new Date(at), { locale: t("locale.format") }),
      })}
    </p>
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
  const unread = entry.readAt === null;
  return (
    <li
      data-fresh={fresh || undefined}
      className={cn(
        "relative grid grid-cols-[8px_minmax(0,1fr)] gap-x-2.5 rounded-md px-3 py-2.5 transition-colors hover:bg-accent motion-reduce:transition-none",
        unread && "bg-primary-subtle",
        fresh &&
          "bg-accent motion-safe:animate-in motion-safe:slide-in-from-top-2",
      )}
    >
      <span
        aria-hidden="true"
        className={cn("mt-[5px] size-2 rounded-full", unread && "bg-primary")}
      />
      <div className="flex min-w-0 flex-col gap-0.5">
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
        {/* One line: the organisation and the time (3.01). The time sits above the link's hit area, so its tooltip opens (F18). */}
        <p className="text-status [overflow-wrap:anywhere] text-muted-foreground">
          {entry.organisation && (
            <>
              <bdi>{entry.organisation}</bdi>
              {" · "}
            </>
          )}
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
