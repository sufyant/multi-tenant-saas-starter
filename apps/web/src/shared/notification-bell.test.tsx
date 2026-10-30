import {
  BuiltInRole,
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
import { northwind, renderInTenant, tenantAnswers } from "../../test/tenant";
import { PersonalBell, TenantBell } from "./notification-bell";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => vi.clearAllMocks());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const joined: NotificationResponse = {
  id: "0198f0a2-0000-7000-8000-0000000000a1",
  type: NotificationType.MemberJoined,
  category: NotificationCategory.MemberInvitations,
  title: "Deniz Arslan joined Northwind Traders",
  body: "Deniz Arslan accepted your invitation and joined as Admin.",
  link: {
    kind: NotificationLinkResponseKind.TenantMembers,
    tenantId: northwind.id,
  },
  createdAt: "2026-10-16T13:29:00Z",
  readAt: null,
};

const removed: NotificationResponse = {
  ...joined,
  id: "0198f0a2-0000-7000-8000-0000000000a2",
  type: NotificationType.MemberRemoved,
  category: NotificationCategory.Access,
  title: "You were removed from Atlas Logistics",
  body: "Ayşe Yılmaz removed you from Atlas Logistics.",
  link: { kind: NotificationLinkResponseKind.Start, tenantId: null },
};

const page = (items: NotificationResponse[]) =>
  HttpResponse.json({ items, nextCursor: null });

/** The tenant bell's reads: its count, which counts its reads, and its list. */
function tenantBell(reads = { count: 0 }) {
  return [
    http.get(`*/v1/tenants/${northwind.id}/notifications/unread-count`, () => {
      reads.count += 1;
      return HttpResponse.json({ unread: 3 });
    }),
    http.get(`*/v1/tenants/${northwind.id}/notifications`, () =>
      page([joined]),
    ),
  ];
}

describe("TenantBell", () => {
  it("TenantBell_Count_ReadsTheTenantsUnreadCount", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), ...tenantBell());

    renderInTenant(<TenantBell tenantId={northwind.id} />);

    expect(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    ).toBeInTheDocument();
  });

  it("TenantBell_Opened_ListsTheTenantsNotifications", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), ...tenantBell());
    renderInTenant(<TenantBell tenantId={northwind.id} />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    );

    expect(
      await screen.findByRole("link", { name: /Deniz Arslan joined/ }),
    ).toHaveAttribute("href", "/northwind/settings/members");
  });

  it("TenantBell_ItemOpened_FollowsItsLinkBeforeTheReadIsAnswered", async () => {
    let release = () => {};
    const marked: string[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      ...tenantBell(),
      http.post("*/v1/me/notifications/:id/read", async ({ params }) => {
        marked.push(String(params.id));
        await new Promise<void>((resolve) => {
          release = resolve;
        });
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderInTenant(<TenantBell tenantId={northwind.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    );

    await userEvent.click(
      await screen.findByRole("link", { name: /Deniz Arslan joined/ }),
    );

    expect(push).toHaveBeenCalledWith("/northwind/settings/members");
    await vi.waitFor(() => expect(marked).toEqual([joined.id]));
    release();
  });

  it("TenantBell_MarkAll_MarksTheTenantsBellAndReadsItAgain", async () => {
    const reads = { count: 0 };
    const marked: string[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      ...tenantBell(reads),
      http.post("*/v1/tenants/:tenantId/notifications/read", ({ params }) => {
        marked.push(String(params.tenantId));
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderInTenant(<TenantBell tenantId={northwind.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    );

    await userEvent.click(
      await screen.findByRole("button", { name: "Mark all as read" }),
    );

    await vi.waitFor(() => expect(reads.count).toBe(2));
    expect(marked).toEqual([northwind.id]);
  });
});

describe("PersonalBell", () => {
  it("PersonalBell_Opened_ListsThePersonalNotifications", async () => {
    server.use(
      http.get("*/v1/me", () => HttpResponse.json({ timeZone: null })),
      http.get("*/v1/me/tenants", () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 100, total: 0 }),
      ),
      http.get("*/v1/me/notifications/unread-count", () =>
        HttpResponse.json({ unread: 1 }),
      ),
      http.get("*/v1/me/notifications", () => page([removed])),
    );
    renderWithProviders(<PersonalBell />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 1 unread" }),
    );

    expect(
      await screen.findByRole("link", { name: /You were removed/ }),
    ).toHaveAttribute("href", "/");
  });
});
