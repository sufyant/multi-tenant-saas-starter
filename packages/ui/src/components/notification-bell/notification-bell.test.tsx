import { ErrorCode } from "@repo/api-client";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, describe, expect, it, vi } from "vitest";
import en from "../../../messages/en.json";
import {
  NotificationBell,
  type NotificationBellProps,
  type NotificationEntry,
} from "./notification-bell";

// Each app holds the locale's format; the bell reads it as the app's pages do. The popover's positioner stays transparent until
// Floating UI places it, which jsdom never does, so a test checks that the list holds an element, not that it is visible.
const messages = { ...en, locale: { format: "en-IE" } };
const now = "2026-10-16T13:32:00Z";

const entry = (
  id: string,
  title: string,
  readAt: string | null = null,
): NotificationEntry => ({
  id,
  title,
  body: `${title}: the body.`,
  createdAt: "2026-10-16T13:29:00Z",
  readAt,
  href: `/northwind/${id}`,
});

const joined = entry("a", "Deniz Arslan joined Northwind Traders");
const plan = entry("b", "Northwind Traders is now on Pro", now);
const fresh = entry("c", "Your role in Northwind Traders changed");

const loaded = (
  items: NotificationEntry[],
  more: Partial<{ hasMore: boolean; onLoadMore: () => void }> = {},
): NotificationBellProps["list"] => ({
  status: "success",
  items,
  hasMore: more.hasMore ?? false,
  loadingMore: false,
  onLoadMore: more.onLoadMore ?? vi.fn(),
});

function bell(props: Partial<NotificationBellProps> = {}) {
  return (
    <NextIntlClientProvider
      locale="en"
      messages={messages}
      timeZone="Europe/Dublin"
    >
      <NotificationBell
        unread={3}
        open
        onOpenChange={vi.fn()}
        list={loaded([joined, plan])}
        onOpenItem={vi.fn()}
        onMarkAll={vi.fn()}
        markingAll={false}
        timeZone="Europe/Dublin"
        now={now}
        {...props}
      />
    </NextIntlClientProvider>
  );
}

/** The badge's text, or null without a badge. */
const countOf = (button: HTMLElement) =>
  button.querySelector("[data-slot='bell-count']")?.textContent ?? null;

const scrollOf = () =>
  screen
    .getByRole("list", { name: "Notifications" })
    .closest("[data-slot='notification-scroll']") as HTMLElement;

/** A screen under bp-tablet: the bell opens its full screen. */
const asAPhone = () => {
  document.documentElement.style.setProperty("--bp-tablet", "768px");
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: false,
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
};

afterEach(() => {
  vi.unstubAllGlobals();
  document.documentElement.style.removeProperty("--bp-tablet");
});

describe("NotificationBell", () => {
  it.each([
    [0, "Notifications", null],
    [3, "Notifications, 3 unread", "3"],
    [99, "Notifications, 99 unread", "99"],
    [100, "Notifications, 99+ unread", "99+"],
  ])("NotificationBell_Unread%i_SaysTheCount", (unread, name, shown) => {
    render(bell({ unread, open: false }));

    expect(countOf(screen.getByRole("button", { name }))).toBe(shown);
  });

  it("NotificationBell_Closed_OpensOnPress", async () => {
    const onOpenChange = vi.fn();
    render(bell({ open: false, onOpenChange }));

    await userEvent.click(
      screen.getByRole("button", { name: "Notifications, 3 unread" }),
    );

    expect(onOpenChange).toHaveBeenCalledWith(true);
  });

  it("NotificationBell_Items_ShowTheTitleTheBodyAndTheRelativeTime", () => {
    render(bell());

    const item = screen.getByRole("link", { name: /Deniz Arslan joined/ });
    const row = item.closest("li") as HTMLElement;
    expect(item).toHaveAttribute("href", "/northwind/a");
    expect(
      within(row).getByText(`${joined.title}: the body.`),
    ).toBeInTheDocument();
    expect(within(row).getByText("3 minutes ago")).toBeInTheDocument();
  });

  it("NotificationBell_UnreadItem_SaysUnreadBesideTheMark", () => {
    render(bell());

    const unread = screen
      .getByRole("link", { name: /Deniz Arslan/ })
      .closest("li");
    const read = screen.getByRole("link", { name: /now on Pro/ }).closest("li");
    expect(
      within(unread as HTMLElement).getByText("Unread"),
    ).toBeInTheDocument();
    expect(within(read as HTMLElement).queryByText("Unread")).toBeNull();
  });

  it("NotificationBell_ItemPressed_OpensIt", async () => {
    const onOpenItem = vi.fn();
    render(bell({ onOpenItem }));

    await userEvent.click(screen.getByRole("link", { name: /Deniz Arslan/ }));

    expect(onOpenItem).toHaveBeenCalledWith(joined, { follow: true });
  });

  it("NotificationBell_ItemPressedWithCtrl_LeavesTheTabWhereItIs", async () => {
    const onOpenItem = vi.fn();
    render(bell({ onOpenItem }));

    // One user-event session keeps Control held for the click.
    const user = userEvent.setup();
    await user.keyboard("{Control>}");
    await user.click(screen.getByRole("link", { name: /Deniz Arslan/ }));

    expect(onOpenItem).toHaveBeenCalledWith(joined, { follow: false });
  });

  it("NotificationBell_MarkAll_MarksWhatTheBellShows", async () => {
    const onMarkAll = vi.fn();
    render(bell({ onMarkAll }));

    await userEvent.click(
      screen.getByRole("button", { name: "Mark all as read" }),
    );

    expect(onMarkAll).toHaveBeenCalledOnce();
  });

  it("NotificationBell_NothingUnread_HasNoMarkAll", () => {
    render(bell({ unread: 0 }));

    expect(
      screen.queryByRole("button", { name: "Mark all as read" }),
    ).toBeNull();
  });

  it("NotificationBell_MorePages_LoadsMore", async () => {
    const onLoadMore = vi.fn();
    render(bell({ list: loaded([joined], { hasMore: true, onLoadMore }) }));

    await userEvent.click(screen.getByRole("button", { name: "Load more" }));

    expect(onLoadMore).toHaveBeenCalledOnce();
    expect(screen.queryByText("No older notifications.")).toBeNull();
  });

  it("NotificationBell_LastPage_SaysThereIsNothingOlder", () => {
    render(bell());

    expect(screen.getByText("No older notifications.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Load more" })).toBeNull();
  });

  it("NotificationBell_Loading_SaysItLoads", () => {
    render(bell({ list: { status: "loading" } }));

    expect(
      screen.getByRole("status", { name: "Loading notifications" }),
    ).toBeInTheDocument();
  });

  it("NotificationBell_Empty_SaysThereAreNone", () => {
    render(bell({ unread: 0, list: loaded([]) }));

    expect(screen.getByText("No notifications")).toBeInTheDocument();
    expect(
      screen.getByText("When something changes for you, it shows here."),
    ).toBeInTheDocument();
  });

  it("NotificationBell_Error_ShowsTheReferenceAndTryAgain", async () => {
    const onRetry = vi.fn();
    render(
      bell({
        list: {
          status: "error",
          problem: { code: ErrorCode.ServerError, reference: "4c19ab" },
          at: Date.parse(now),
          onRetry,
        },
      }),
    );

    expect(screen.getByText("Notifications didn’t load")).toBeInTheDocument();
    expect(screen.getByText("Ref 4c19ab · 13:32 UTC")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("NotificationBell_Offline_KeepsTheListAndMarkAllWaits", () => {
    vi.stubGlobal("navigator", { ...navigator, onLine: false });
    render(bell());

    expect(screen.getByText("You’re offline.")).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /Deniz Arslan/ }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Mark all as read" }),
    ).toBeDisabled();
  });

  it("NotificationBell_NewItemWhileScrolled_KeepsTheListStillAndOffersIt", async () => {
    const { rerender } = render(bell());
    act(() => {
      scrollOf().scrollTop = 120;
    });

    rerender(bell({ list: loaded([fresh, joined, plan]) }));

    expect(screen.queryByRole("link", { name: /Your role/ })).toBeNull();
    expect(screen.getByRole("status")).toHaveTextContent("1 new notification");
    await userEvent.click(screen.getByRole("button", { name: "1 new" }));
    expect(screen.getByRole("link", { name: /Your role/ })).toBeInTheDocument();
    expect(scrollOf().scrollTop).toBe(0);
  });

  it("NotificationBell_NewItemAtTheTop_ComesInFirstHighlighted", () => {
    const { rerender } = render(bell());

    rerender(bell({ list: loaded([fresh, joined, plan]) }));

    const links = screen.getAllByRole("link");
    expect(links[0]).toHaveAccessibleName(/Your role/);
    expect(links[0]?.closest("li")).toHaveAttribute("data-fresh");
    expect(screen.getByRole("status")).toHaveTextContent("1 new notification");
    expect(screen.queryByRole("button", { name: "1 new" })).toBeNull();
  });

  it("NotificationBell_OlderPageLoaded_AnnouncesNothing", () => {
    const { rerender } = render(bell({ list: loaded([joined]) }));

    rerender(bell({ list: loaded([joined, plan]) }));

    expect(screen.getByRole("status")).toHaveTextContent("");
  });

  it("NotificationBell_Phone_OpensAFullScreenWithCloseAtTheStart", async () => {
    asAPhone();
    const onOpenChange = vi.fn();
    render(bell({ onOpenChange }));

    const screenDialog = screen.getByRole("dialog", { name: "Notifications" });
    const close = within(screenDialog).getByRole("button", { name: "Close" });
    await userEvent.click(close);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  // The full screen is Base UI's Dialog, which has no swipe to dismiss (Base UI's Drawer has one). A drag down the list scrolls it.
  it("NotificationBell_PhoneListDraggedDown_StaysOpen", () => {
    asAPhone();
    const onOpenChange = vi.fn();
    render(bell({ onOpenChange }));
    const list = scrollOf();

    fireEvent.pointerDown(list, { pointerType: "touch", clientY: 100 });
    fireEvent.touchStart(list, { touches: [{ clientX: 50, clientY: 100 }] });
    fireEvent.pointerMove(list, { pointerType: "touch", clientY: 600 });
    fireEvent.touchMove(list, { touches: [{ clientX: 50, clientY: 600 }] });
    list.scrollTop = 300;
    fireEvent.scroll(list);
    fireEvent.touchEnd(list, {
      changedTouches: [{ clientX: 50, clientY: 600 }],
    });
    fireEvent.pointerUp(list, { pointerType: "touch", clientY: 600 });

    expect(onOpenChange).not.toHaveBeenCalled();
    expect(
      screen.getByRole("dialog", { name: "Notifications" }),
    ).toBeInTheDocument();
  });

  it("NotificationBell_Open_HasNoAccessibilityViolation", async () => {
    render(bell());

    // jsdom draws nothing, so the rules of colour and of page regions stay out; jsdom counts Base UI's focus guards as focusable
    // inside aria-hidden, and Chromium's axe does not (F37).
    const result = await axe.run(
      { include: [document.body], exclude: [["[data-base-ui-focus-guard]"]] },
      {
        rules: {
          "color-contrast": { enabled: false },
          region: { enabled: false },
        },
      },
    );
    expect(result.violations.map((violation) => violation.id)).toEqual([]);
  });
});
