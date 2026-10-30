import {
  NotificationCategory,
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
  category: NotificationCategory.PlatformTenants,
  title: "Northwind Traders was suspended",
  body: "Ciara Byrne suspended Northwind Traders.",
  link: { kind: NotificationLinkResponseKind.PlatformTenant, tenantId },
  createdAt: "2026-10-16T13:29:00Z",
  readAt: null,
};

/** The platform bell's reads: its count, which counts its reads, and its list. */
function platformBell(reads = { count: 0 }) {
  return [
    http.get("*/v1/me", () => HttpResponse.json({ timeZone: null })),
    http.get("*/v1/platform/notifications/unread-count", () => {
      reads.count += 1;
      return HttpResponse.json({ unread: 2 });
    }),
    http.get("*/v1/platform/notifications", () =>
      HttpResponse.json({ items: [suspended], nextCursor: null }),
    ),
  ];
}

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
