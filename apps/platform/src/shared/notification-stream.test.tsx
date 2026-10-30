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
import { renderWithProviders } from "../../test/render";
import { PlatformBell } from "./notification-bell";
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

/** The platform bell's count, which says how often it was read. */
async function platformBellWithStream() {
  const reads = { count: 0 };
  server.use(
    http.get("*/v1/me", () => HttpResponse.json({ timeZone: null })),
    http.get("*/v1/platform/notifications/unread-count", () => {
      reads.count += 1;
      return HttpResponse.json({ unread: reads.count });
    }),
  );
  renderWithProviders(
    <>
      <NotificationStream />
      <PlatformBell />
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
  it("NotificationStream_PlatformSignal_ReadsThePlatformBellAgain", async () => {
    await platformBellWithStream();

    signal({ scope: "platform" });

    expect(
      await screen.findByRole("button", { name: "Notifications, 2 unread" }),
    ).toBeInTheDocument();
  });

  it("NotificationStream_TenantSignal_ReadsNothing", async () => {
    const reads = await platformBellWithStream();

    signal({
      scope: "tenant",
      tenantId: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
    });

    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(reads.count).toBe(1);
  });

  it("NotificationStream_Focus_ReadsTheBellAgain", async () => {
    await platformBellWithStream();

    act(() => {
      window.dispatchEvent(new Event("focus"));
    });

    expect(
      await screen.findByRole("button", { name: "Notifications, 2 unread" }),
    ).toBeInTheDocument();
  });
});
