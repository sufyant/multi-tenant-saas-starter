import {
  BuiltInRole,
  ErrorCode,
  NotificationLinkResponseKind,
  type NotificationResponse,
  NotificationType,
} from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, within } from "@testing-library/react";
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
  title: "Deniz Arslan joined Northwind Traders",
  body: "Deniz Arslan accepted your invitation and joined as Admin.",
  area: "Members",
  actorName: "Deniz Arslan",
  tenantName: "Northwind Traders",
  link: {
    kind: NotificationLinkResponseKind.TenantMembers,
    tenantId: northwind.id,
  },
  createdAt: "2026-10-16T13:29:00Z",
  readAt: null,
  unread: true,
};

const removed: NotificationResponse = {
  ...joined,
  id: "0198f0a2-0000-7000-8000-0000000000a2",
  type: NotificationType.MemberRemoved,
  title: "You were removed from Atlas Logistics",
  body: "Ayşe Yılmaz removed you from Atlas Logistics.",
  link: { kind: NotificationLinkResponseKind.Start, tenantId: null },
};

const page = (items: NotificationResponse[]) =>
  HttpResponse.json({ items, nextCursor: null });

/** The tenant bell's reads: its count, which counts its reads, and its list, which keeps the filter of each read. */
function tenantBell(
  reads = { count: 0 },
  filters: (string | null)[] = [],
  unread = 3,
) {
  return [
    http.get(`*/v1/tenants/${northwind.id}/notifications/unread-count`, () => {
      reads.count += 1;
      return HttpResponse.json({ unread });
    }),
    http.get(`*/v1/tenants/${northwind.id}/notifications`, ({ request }) => {
      filters.push(new URL(request.url).searchParams.get("unread"));
      return page([joined]);
    }),
  ];
}

/** A row's meta line: the one paragraph under its title. */
const metaOf = (title: HTMLElement) => title.closest("li")?.querySelector("p");

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

  // The name the API gives with the item, as it was at that moment; the list of organisations may name it otherwise now.
  it("TenantBell_Opened_NamesEachItemsOrganisationAndLeavesTheBody", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      http.get(`*/v1/tenants/${northwind.id}/notifications/unread-count`, () =>
        HttpResponse.json({ unread: 3 }),
      ),
      http.get(`*/v1/tenants/${northwind.id}/notifications`, () =>
        page([{ ...joined, tenantName: "Northwind Trading Co." }]),
      ),
    );
    renderInTenant(<TenantBell tenantId={northwind.id} />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    );

    const item = await screen.findByRole("link", { name: /Deniz Arslan/ });
    expect(
      within(item.closest("li") as HTMLElement).getByText(
        "Northwind Trading Co.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText(joined.body)).toBeNull();
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

describe("TenantBell tabs", () => {
  // 3.01: with unread items the bell opens on Unread, which reads the list with the filter unread.
  it("TenantBell_OpenedWithUnread_ReadsTheUnreadTab", async () => {
    const filters: (string | null)[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      ...tenantBell({ count: 0 }, filters),
    );
    renderInTenant(<TenantBell tenantId={northwind.id} />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    );

    expect(
      await screen.findByRole("tab", { name: "Unread 3", selected: true }),
    ).toBeInTheDocument();
    await screen.findByRole("link", { name: /Deniz Arslan joined/ });
    expect(filters).toEqual(["true"]);
  });

  // 3.03: with nothing unread the bell opens on All.
  it("TenantBell_OpenedWithNothingUnread_ReadsTheAllTab", async () => {
    const filters: (string | null)[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      ...tenantBell({ count: 0 }, filters, 0),
    );
    renderInTenant(<TenantBell tenantId={northwind.id} />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications" }),
    );

    expect(
      await screen.findByRole("tab", { name: "All", selected: true }),
    ).toBeInTheDocument();
    await screen.findByRole("link", { name: /Deniz Arslan joined/ });
    expect(filters).toEqual([null]);
  });

  it("TenantBell_AllPressed_ReadsEveryItem", async () => {
    const filters: (string | null)[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      ...tenantBell({ count: 0 }, filters),
    );
    renderInTenant(<TenantBell tenantId={northwind.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    );
    await screen.findByRole("link", { name: /Deniz Arslan joined/ });

    await userEvent.click(screen.getByRole("tab", { name: "All" }));

    await vi.waitFor(() => expect(filters).toEqual(["true", null]));
  });

  // 3.01 leads to 3.09: Mark all as read shows the All tab.
  it("TenantBell_MarkAllAnswered_ShowsTheAllTab", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      ...tenantBell(),
      http.post("*/v1/tenants/:tenantId/notifications/read", () => {
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

    expect(
      await screen.findByRole("tab", { name: /^All/, selected: true }),
    ).toBeInTheDocument();
  });

  // The tenant web's line: the organisation, the area and the time; the person who acted stays in the title (3.01).
  it("TenantBell_Opened_ShowsTheOrganisationAndTheAreaWithoutTheActor", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), ...tenantBell());
    renderInTenant(<TenantBell tenantId={northwind.id} />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    );

    const meta = metaOf(
      await screen.findByRole("link", { name: /Deniz Arslan joined/ }),
    );
    expect(meta).toHaveTextContent(/^Northwind Traders · Members · /);
    expect(meta).not.toHaveTextContent("Deniz Arslan");
  });
});

describe("TenantBell failures", () => {
  it("TenantBell_ItemOpenedInANewTab_MarksItReadAndStays", async () => {
    const marked: string[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      ...tenantBell(),
      http.post("*/v1/me/notifications/:id/read", ({ params }) => {
        marked.push(String(params.id));
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderInTenant(<TenantBell tenantId={northwind.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    );
    const item = await screen.findByRole("link", { name: /Deniz Arslan/ });

    // One user-event session keeps Control held for the click.
    const user = userEvent.setup();
    await user.keyboard("{Control>}");
    await user.click(item);

    await vi.waitFor(() => expect(marked).toEqual([joined.id]));
    expect(push).not.toHaveBeenCalled();
  });

  it("TenantBell_MarkAllFails_SaysSoWithTheReason", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      ...tenantBell(),
      http.post("*/v1/tenants/:tenantId/notifications/read", () =>
        HttpResponse.json(
          {
            status: 500,
            title: "Error",
            code: ErrorCode.ServerError,
            traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
          },
          { status: 500 },
        ),
      ),
    );
    renderInTenant(<TenantBell tenantId={northwind.id} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Notifications, 3 unread" }),
    );

    await userEvent.click(
      await screen.findByRole("button", { name: "Mark all as read" }),
    );

    await expectVisibleToast("Notifications not marked as read");
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
