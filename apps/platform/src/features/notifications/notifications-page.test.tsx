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
import { renderWithProviders } from "../../../test/render";
import { PlatformNotifications } from "./notifications-page";

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

/** The platform bell's reads: its count and its list, which keeps the filter of each read. */
function platformBell(filters: (string | null)[] = []) {
  return [
    http.get("*/v1/me", () => HttpResponse.json({ timeZone: null })),
    http.get("*/v1/platform/notifications/unread-count", () =>
      HttpResponse.json({ unread: 2 }),
    ),
    http.get("*/v1/platform/notifications", ({ request }) => {
      filters.push(new URL(request.url).searchParams.get("unread"));
      return HttpResponse.json({ items: [suspended], nextCursor: null });
    }),
  ];
}

describe("PlatformNotifications", () => {
  it("PlatformNotifications_Shown_ListsTheUnreadOnesWithMarkAll", async () => {
    const filters: (string | null)[] = [];
    const marked: string[] = [];
    server.use(
      ...platformBell(filters),
      http.post("*/v1/platform/notifications/read", () => {
        marked.push("platform");
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<PlatformNotifications />);

    expect(
      await screen.findByRole("link", { name: /was suspended/ }),
    ).toHaveAttribute("href", `/organisations/${tenantId}`);
    expect(
      screen.getByRole("tab", { name: "Unread 2", selected: true }),
    ).toBeInTheDocument();
    expect(filters).toEqual(["true"]);
    await userEvent.click(
      screen.getByRole("button", { name: "Mark all as read" }),
    );
    await vi.waitFor(() => expect(marked).toEqual(["platform"]));
  });

  // F57: the last unread item, read in a new tab, stays drawn as read, and so does the tab.
  it("PlatformNotifications_LastUnreadReadInANewTab_KeepsTheTabAndTheItem", async () => {
    const read = { done: false };
    server.use(
      http.get("*/v1/me", () => HttpResponse.json({ timeZone: null })),
      http.get("*/v1/platform/notifications/unread-count", () =>
        HttpResponse.json({ unread: read.done ? 0 : 1 }),
      ),
      http.get("*/v1/platform/notifications", () =>
        HttpResponse.json({
          items: read.done ? [] : [suspended],
          nextCursor: null,
        }),
      ),
      http.post("*/v1/me/notifications/:id/read", () => {
        read.done = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<PlatformNotifications />);
    const item = await screen.findByRole("link", { name: /was suspended/ });

    // One user-event session keeps Control held for the click.
    const user = userEvent.setup();
    await user.keyboard("{Control>}");
    await user.click(item);

    expect(
      await screen.findByRole("tab", { name: "Unread", selected: true }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /was suspended/ }),
    ).not.toHaveAccessibleName(/^Unread/);
  });
});
