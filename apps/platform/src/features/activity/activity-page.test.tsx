import { AuditOperation, ErrorCode } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
} from "@repo/api-client/msw";
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
import {
  auditLogAnswer,
  entry,
  tenantTarget,
  thePlatform,
} from "../../../test/activity";
import { corrib, problem } from "../../../test/organisations";
import { renderWithProviders } from "../../../test/render";
import { ActivityPage } from "./activity-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
// Only Date is fake: "now" is 16 Oct 2026, 14:32 in Dublin, the boards' moment.
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-16T13:32:00Z"));
});
afterEach(() => {
  vi.useRealTimers();
  server.resetHandlers();
});
afterAll(() => server.close());

/** GetMe of the system admin, in English and the time zone given. */
const meIn = (timeZone = "Europe/Dublin") =>
  getGetMeMockHandler(
    getGetMeResponseMock({ isSystemAdmin: true, language: "en", timeZone }),
  );

const suspendedJohn = entry();
const reactivated = entry({
  id: "0198f0a2-0000-7000-8000-0000000000e2",
  occurredAt: "2026-10-15T16:20:00Z",
  operation: AuditOperation.TenantReactivated,
  target: tenantTarget(),
});
const purged = entry({
  id: "0198f0a2-0000-7000-8000-0000000000e3",
  occurredAt: "2026-10-09T09:00:00Z",
  operation: AuditOperation.TenantPurged,
  actor: thePlatform,
  target: tenantTarget(corrib),
});

/** The day headings and, under each, the text of its entries, in order. */
const days = async () => {
  await screen.findAllByRole("heading", { level: 2 });
  return screen.getAllByRole("heading", { level: 2 }).map((heading) => [
    heading.textContent,
    within(heading.nextElementSibling as HTMLElement)
      .getAllByRole("listitem")
      .map((item) => item.querySelector("p")?.textContent),
  ]);
};

const findSentence = (text: string) =>
  screen.findByText((_, element) => element?.textContent === text, {
    selector: "p",
  });

/** The first page answers with a next cursor; a request with a cursor is refused with the status given. */
const refusesEveryCursor = (cursors: (string | null)[], status: number) =>
  http.get("*/v1/platform/audit-log", ({ request }) => {
    const cursor = new URL(request.url).searchParams.get("cursor");
    cursors.push(cursor);
    return cursor
      ? problem(
          status,
          {
            400: ErrorCode.PagingCursorInvalid,
            401: ErrorCode.AuthUnauthenticated,
            403: ErrorCode.UserSuspended,
          }[status] ?? ErrorCode.ServerError,
        )
      : HttpResponse.json({ items: [suspendedJohn], nextCursor: "page-2" });
  });

describe("ActivityPage", () => {
  it("ActivityPage_Entries_GroupsThemByDayNewestFirst", async () => {
    server.use(
      meIn(),
      auditLogAnswer({
        "": { items: [suspendedJohn, reactivated, purged], nextCursor: null },
      }),
    );

    renderWithProviders(<ActivityPage />);

    expect(await days()).toEqual([
      ["Today, 16 Oct 2026", ["Ciara Byrne suspended the user John Byrne."]],
      [
        "Yesterday, 15 Oct 2026",
        ["Ciara Byrne reactivated Northwind Traders."],
      ],
      ["9 Oct 2026", ["Corrib Press was deleted."]],
    ]);
    expect(
      screen.getByRole("heading", { level: 1, name: "Activity" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Updated", { exact: false })).toBeInTheDocument();
  });

  it("ActivityPage_WholeLog_SendsNoTenantId", async () => {
    const requests: URL[] = [];
    server.use(
      meIn(),
      auditLogAnswer(
        { "": { items: [suspendedJohn], nextCursor: null } },
        requests,
      ),
    );

    renderWithProviders(<ActivityPage />);

    expect(await screen.findByRole("listitem")).toBeInTheDocument();
    expect(requests.map((url) => url.searchParams.has("tenantId"))).toEqual([
      false,
    ]);
  });

  // 22:30 and 23:30 UTC on the 15th: before and after Dublin's midnight; 20:30 and 21:30 UTC: before and after Riyadh's.
  it.each([
    ["Europe/Dublin", "2026-10-15T23:30:00Z", "2026-10-15T22:30:00Z"],
    ["Asia/Riyadh", "2026-10-15T21:30:00Z", "2026-10-15T20:30:00Z"],
  ])(
    "ActivityPage_MidnightIn%s_PutsTheEntriesOnTwoDays",
    async (timeZone, after, before) => {
      server.use(
        meIn(timeZone),
        auditLogAnswer({
          "": {
            items: [
              entry({ occurredAt: after }),
              { ...reactivated, occurredAt: before },
            ],
            nextCursor: null,
          },
        }),
      );

      renderWithProviders(<ActivityPage />);

      expect(await days()).toEqual([
        ["Today, 16 Oct 2026", ["Ciara Byrne suspended the user John Byrne."]],
        [
          "Yesterday, 15 Oct 2026",
          ["Ciara Byrne reactivated Northwind Traders."],
        ],
      ]);
    },
  );

  it("ActivityPage_LoadOlderActivity_SendsTheCursorAppendsThePageAndDisappearsAtTheEnd", async () => {
    const requests: URL[] = [];
    server.use(
      meIn(),
      auditLogAnswer(
        {
          "": { items: [suspendedJohn, reactivated], nextCursor: "page-2" },
          "page-2": { items: [purged], nextCursor: null },
        },
        requests,
      ),
    );
    renderWithProviders(<ActivityPage />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Load older activity" }),
    );

    expect(await findSentence("Corrib Press was deleted.")).toBeInTheDocument();
    expect(
      await findSentence("Ciara Byrne suspended the user John Byrne."),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Load older activity" }),
    ).not.toBeInTheDocument();
    expect(requests.map((url) => url.searchParams.get("cursor"))).toEqual([
      null,
      "page-2",
    ]);
  });

  it("ActivityPage_LastPage_ShowsNoOlderButton", async () => {
    server.use(
      meIn(),
      auditLogAnswer({ "": { items: [suspendedJohn], nextCursor: null } }),
    );

    renderWithProviders(<ActivityPage />);

    expect(await screen.findByRole("listitem")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Load older activity" }),
    ).not.toBeInTheDocument();
  });

  it("ActivityPage_CursorNoLongerValid_StartsFromTheNewestAndSaysSo", async () => {
    const cursors: (string | null)[] = [];
    server.use(meIn(), refusesEveryCursor(cursors, 400));
    renderWithProviders(<ActivityPage />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Load older activity" }),
    );

    expect(
      await screen.findByText("Starting from the newest activity."),
    ).toBeInTheDocument();
    await vi.waitFor(() => expect(cursors).toEqual([null, "page-2", null]));
  });

  it("ActivityPage_OlderPageFails_SaysSoAndKeepsTheEntries", async () => {
    server.use(meIn(), refusesEveryCursor([], 500));
    renderWithProviders(<ActivityPage />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Load older activity" }),
    );

    expect(
      await screen.findByText("Couldn’t load older activity"),
    ).toBeInTheDocument();
    expect(
      await findSentence("Ciara Byrne suspended the user John Byrne."),
    ).toBeInTheDocument();
  });

  it("ActivityPage_OlderPageSignedOut_GoesToSignIn", async () => {
    const assign = vi.fn();
    vi.stubGlobal("location", { ...window.location, assign });
    server.use(meIn(), refusesEveryCursor([], 401));
    renderWithProviders(<ActivityPage />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Load older activity" }),
    );

    await vi.waitFor(() => expect(assign).toHaveBeenCalledWith("/sign-in"));
    vi.unstubAllGlobals();
  });

  // A refusal of the caller's account is final (F47): no toast; GetMe's answer decides what the shell shows.
  it("ActivityPage_OlderPageAccountSuspended_ShowsNoToastAndReadsGetMeAgain", async () => {
    const cursors: (string | null)[] = [];
    let meReads = 0;
    server.use(
      getGetMeMockHandler(() => {
        meReads += 1;
        return getGetMeResponseMock({
          isSystemAdmin: true,
          language: "en",
          timeZone: "Europe/Dublin",
        });
      }),
      refusesEveryCursor(cursors, 403),
    );
    renderWithProviders(<ActivityPage />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Load older activity" }),
    );

    await vi.waitFor(() => expect(meReads).toBe(2));
    expect(cursors).toEqual([null, "page-2"]);
    expect(
      screen.queryByText("Couldn’t load older activity"),
    ).not.toBeInTheDocument();
  });

  it("ActivityPage_NoEntries_SaysNoActivityYet", async () => {
    server.use(meIn(), auditLogAnswer({ "": { items: [], nextCursor: null } }));

    renderWithProviders(<ActivityPage />);

    expect(
      await screen.findByRole("heading", { name: "No activity yet" }),
    ).toBeInTheDocument();
  });

  it("ActivityPage_LogFails_ShowsTheReferenceAndTryAgainReadsAgain", async () => {
    let calls = 0;
    server.use(
      meIn(),
      http.get("*/v1/platform/audit-log", () => {
        calls += 1;
        return calls === 1
          ? problem(500, ErrorCode.ServerError)
          : HttpResponse.json({ items: [suspendedJohn], nextCursor: null });
      }),
    );
    renderWithProviders(<ActivityPage />);

    expect(
      await screen.findByRole("heading", { name: "Couldn’t load activity" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Ref 0af7651916cd43dd8448eb211c80319c/),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));

    expect(
      await findSentence("Ciara Byrne suspended the user John Byrne."),
    ).toBeInTheDocument();
  });
});
