import { BuiltInRole } from "@repo/api-client";
import { FakeEventSource } from "@repo/ui/test/event-source";
import { act, screen } from "@testing-library/react";
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
import { northwind, renderInTenant, tenantAnswers } from "../../test/tenant";
import { TenantBell } from "./notification-bell";
import { NotificationStream } from "./notification-stream";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => {
  FakeEventSource.opened = [];
  vi.stubGlobal("EventSource", FakeEventSource);
});
afterEach(() => {
  server.resetHandlers();
  vi.unstubAllGlobals();
});
afterAll(() => server.close());

const atlas = "0198f0a2-0000-7000-8000-00000000a71a";

/** The tenant bell's count, which says how often it was read. */
async function tenantBellWithStream() {
  const reads = { count: 0 };
  server.use(
    ...tenantAnswers(BuiltInRole.Owner),
    http.get(`*/v1/tenants/${northwind.id}/notifications/unread-count`, () => {
      reads.count += 1;
      return HttpResponse.json({ unread: reads.count });
    }),
  );
  renderInTenant(
    <>
      <NotificationStream />
      <TenantBell tenantId={northwind.id} />
    </>,
  );
  await screen.findByRole("button", { name: "Notifications, 1 unread" });
  act(() => FakeEventSource.latest().open());
  return reads;
}

const signal = (data: object) =>
  act(() =>
    FakeEventSource.latest().send("notifications", JSON.stringify(data)),
  );

describe("NotificationStream", () => {
  it("NotificationStream_Mounted_OpensOneStreamThroughTheBff", async () => {
    await tenantBellWithStream();

    expect(FakeEventSource.opened).toHaveLength(1);
    expect(FakeEventSource.latest().url).toMatch(
      /\/api\/v1\/me\/notifications\/stream$/,
    );
  });

  it("NotificationStream_ThisTenantsSignal_ReadsTheBellAgain", async () => {
    await tenantBellWithStream();

    signal({ scope: "tenant", tenantId: northwind.id });

    expect(
      await screen.findByRole("button", { name: "Notifications, 2 unread" }),
    ).toBeInTheDocument();
  });

  it("NotificationStream_PersonalSignal_ReadsEveryTenantBellAgain", async () => {
    await tenantBellWithStream();

    signal({ scope: "personal" });

    expect(
      await screen.findByRole("button", { name: "Notifications, 2 unread" }),
    ).toBeInTheDocument();
  });

  it("NotificationStream_AnotherTenantsSignal_ReadsNothing", async () => {
    const reads = await tenantBellWithStream();

    signal({ scope: "tenant", tenantId: atlas });
    signal({ scope: "platform" });

    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(reads.count).toBe(1);
  });

  it.each([["focus"], ["online"]])(
    "NotificationStream_%s_ReadsTheOpenBellAgain",
    async (type) => {
      await tenantBellWithStream();

      act(() => {
        window.dispatchEvent(new Event(type));
      });

      expect(
        await screen.findByRole("button", { name: "Notifications, 2 unread" }),
      ).toBeInTheDocument();
    },
  );

  it("NotificationStream_StreamOpensAgain_ReadsTheOpenBellAgain", async () => {
    await tenantBellWithStream();

    act(() => FakeEventSource.latest().open());

    expect(
      await screen.findByRole("button", { name: "Notifications, 2 unread" }),
    ).toBeInTheDocument();
  });
});
