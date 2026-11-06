import {
  NotificationLinkResponseKind,
  type NotificationResponse,
  NotificationType,
} from "@repo/api-client";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import { renderWithProviders } from "../../test/render";
import { PlatformBell } from "./notification-bell";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => vi.clearAllMocks());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const tenantId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";
const suspended: NotificationResponse = {
  id: "0198f0a2-0000-7000-8000-0000000000b1",
  type: NotificationType.TenantSuspended,
  title: "Northwind Traders was suspended",
  body: "Ciara Byrne suspended Northwind Traders.",
  area: "Organisations",
  actorName: "Ciara Byrne",
  tenantName: "Northwind Traders",
  link: { kind: NotificationLinkResponseKind.PlatformTenant, tenantId },
  createdAt: "2026-10-16T13:29:00Z",
  readAt: null,
  unread: true,
};

/** The platform bell's reads: its count, which counts its reads, and its list, which keeps the filter of each read. */
function platformBell(reads = { count: 0 }, filters: (string | null)[] = []) {
  return [
    http.get("*/v1/me", () => HttpResponse.json({ timeZone: null })),
    http.get("*/v1/platform/notifications/unread-count", () => {
      reads.count += 1;
      return HttpResponse.json({ unread: 2 });
    }),
    http.get("*/v1/platform/notifications", ({ request }) => {
      filters.push(new URL(request.url).searchParams.get("unread"));
      return HttpResponse.json({ items: [suspended], nextCursor: null });
    }),
  ];
}

/** A row's meta line: the one paragraph under its title. */
const metaOf = (title: HTMLElement) => title.closest("li")?.querySelector("p");

/** A screen under bp-tablet. */
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

const openBell = async () =>
  userEvent.click(
    await screen.findByRole("button", { name: "Notifications, 2 unread" }),
  );

describe("PlatformBell", () => {
  it("PlatformBell_Opened_ListsThePlatformsNotifications", async () => {
    server.use(...platformBell());
    renderWithProviders(<PlatformBell />);

    await openBell();

    expect(
      await screen.findByRole("link", { name: /was suspended/ }),
    ).toHaveAttribute("href", `/organisations/${tenantId}`);
  });

  // The platform's line: the area, the person who acted, without a verb, and the time (3.01 of the platform).
  it("PlatformBell_Opened_ShowsTheAreaAndTheActor", async () => {
    server.use(...platformBell());
    renderWithProviders(<PlatformBell />);

    await openBell();

    const meta = metaOf(
      await screen.findByRole("link", { name: /was suspended/ }),
    );
    expect(meta).toHaveTextContent(/^Organisations · Ciara Byrne · /);
    expect(meta).not.toHaveTextContent("Northwind Traders");
  });

  it("PlatformBell_OpenedWithUnread_ReadsTheUnreadTab", async () => {
    const filters: (string | null)[] = [];
    server.use(...platformBell({ count: 0 }, filters));
    renderWithProviders(<PlatformBell />);

    await openBell();

    expect(
      await screen.findByRole("tab", { name: "Unread 2", selected: true }),
    ).toBeInTheDocument();
    await screen.findByRole("link", { name: /was suspended/ });
    expect(filters).toEqual(["true"]);
  });

  // 3.08 of the platform: on a phone the bell opens the page of notifications under the top bar.
  it("PlatformBell_Phone_LinksToThePage", async () => {
    asAPhone();
    server.use(...platformBell());
    renderWithProviders(<PlatformBell />);

    expect(
      await screen.findByRole("link", { name: "Notifications, 2 unread" }),
    ).toHaveAttribute("href", "/notifications");
  });

  it("PlatformBell_ItemOpened_MarksItReadAndFollowsItsLink", async () => {
    const marked: string[] = [];
    server.use(
      ...platformBell(),
      http.post("*/v1/me/notifications/:id/read", ({ params }) => {
        marked.push(String(params.id));
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<PlatformBell />);
    await openBell();

    await userEvent.click(
      await screen.findByRole("link", { name: /was suspended/ }),
    );

    expect(push).toHaveBeenCalledWith(`/organisations/${tenantId}`);
    await vi.waitFor(() => expect(marked).toEqual([suspended.id]));
  });

  it("PlatformBell_MarkAll_MarksThePlatformBellAndReadsItAgain", async () => {
    const reads = { count: 0 };
    const marked: string[] = [];
    server.use(
      ...platformBell(reads),
      http.post("*/v1/platform/notifications/read", () => {
        marked.push("platform");
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<PlatformBell />);
    await openBell();

    await userEvent.click(
      await screen.findByRole("button", { name: "Mark all as read" }),
    );

    await vi.waitFor(() => expect(reads.count).toBe(2));
    expect(marked).toEqual(["platform"]);
  });
});

describe("PlatformBell offline", () => {
  afterEach(() => vi.restoreAllMocks());

  // The browser's signal is only a hint (F59): Chrome on macOS can say offline with some VPNs while every call answers.
  it("PlatformBell_BrowserSaysOfflineWhileTheCallsAnswer_KeepsMarkAll", async () => {
    vi.spyOn(navigator, "onLine", "get").mockReturnValue(false);
    server.use(...platformBell());
    renderWithProviders(<PlatformBell />);
    await userEvent.click(
      await screen.findByRole("button", {
        name: /^Notifications, \d+ unread$/,
      }),
    );

    expect(
      await screen.findByRole("button", { name: "Mark all as read" }),
    ).toBeEnabled();
    expect(screen.queryByText("You’re offline.")).not.toBeInTheDocument();
  });

  it("PlatformBell_CallsGetNoAnswerWhileOffline_ShowsTheOfflineRow", async () => {
    vi.spyOn(navigator, "onLine", "get").mockReturnValue(false);
    server.use(http.get("*", () => HttpResponse.error()));
    renderWithProviders(<PlatformBell />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications" }),
    );

    expect(await screen.findByText("You’re offline.")).toBeInTheDocument();
  });
});
