import {
  AuditActorResponseKind,
  AuditOperation,
  ErrorCode,
  MemberRole,
} from "@repo/api-client";
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
import { auditLogAnswer, detailsOf, entry } from "../../../test/activity";
import {
  meOf,
  northwind,
  problem,
  renderInTenant,
  tenantAnswers,
} from "../../../test/tenant";
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

const url = "/northwind/settings/activity";

const joined = entry({ occurredAt: "2026-10-16T08:40:00Z" });
const invited = entry({
  id: "0198f0a2-0000-7000-8000-0000000000e2",
  occurredAt: "2026-10-16T13:32:00Z",
  operation: AuditOperation.MemberInvited,
  details: detailsOf[AuditOperation.MemberInvited],
});
const revokedByDeleted = entry({
  id: "0198f0a2-0000-7000-8000-0000000000e3",
  occurredAt: "2026-10-15T07:15:00Z",
  operation: AuditOperation.InvitationRevoked,
  actor: {
    kind: AuditActorResponseKind.User,
    userId: "0198f0a2-0000-7000-8000-000000000009",
    displayName: null,
    deleted: true,
  },
});
const reactivated = entry({
  id: "0198f0a2-0000-7000-8000-0000000000e4",
  occurredAt: "2026-10-15T16:20:00Z",
  operation: AuditOperation.TenantReactivated,
  actor: {
    kind: AuditActorResponseKind.Platform,
    userId: null,
    displayName: null,
    deleted: null,
  },
});
const created = entry({
  id: "0198f0a2-0000-7000-8000-0000000000e5",
  occurredAt: "2026-10-01T09:00:00Z",
  operation: AuditOperation.TenantCreated,
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

/** The tenant's answers for a viewer of the role, whose time zone is the one given. The first handler that matches answers. */
const answer = (role: MemberRole, timeZone = "Europe/Dublin") => [
  http.get("*/v1/me", () => HttpResponse.json({ ...meOf(role), timeZone })),
  ...tenantAnswers(role),
];

/** The paragraph of an entry's sentence: the names inside it are elements of their own. */
const isSentence = (text: string) => (_: string, element: Element | null) =>
  element?.tagName === "P" && element.textContent === text;
const findSentence = (text: string) => screen.findByText(isSentence(text));
const getSentence = (text: string) => screen.getByText(isSentence(text));

/** The first page answers with a next cursor; a request with a cursor answers 400 paging.cursor_invalid. */
const refusesEveryCursor = (cursors: (string | null)[]) =>
  http.get("*/v1/tenants/:tenantId/audit-log", ({ request }) => {
    const cursor = new URL(request.url).searchParams.get("cursor");
    cursors.push(cursor);
    return cursor
      ? problem(400, ErrorCode.PagingCursorInvalid)
      : HttpResponse.json({ items: [joined], nextCursor: "page-2" });
  });

/** The first read fails with a server error; the next answers one entry. */
const failsOnce = () => {
  let calls = 0;
  return http.get("*/v1/tenants/:tenantId/audit-log", () => {
    calls += 1;
    return calls === 1
      ? problem(500, ErrorCode.ServerError)
      : HttpResponse.json({ items: [joined], nextCursor: null });
  });
};

describe("ActivityPage", () => {
  it("ActivityPage_Owner_GroupsTheEntriesByDayNewestFirst", async () => {
    server.use(
      ...answer(MemberRole.Owner),
      auditLogAnswer({
        "": {
          items: [invited, joined, reactivated, revokedByDeleted, created],
          nextCursor: null,
        },
      }),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(await days()).toEqual([
      [
        "Today, 16 Oct 2026",
        [
          "Ayşe Yılmaz invited someone as Member.",
          "Khalid Al-Harbi joined the organisation.",
        ],
      ],
      [
        "Yesterday, 15 Oct 2026",
        [
          "Platform The platform reactivated this organisation.",
          "Deleted user revoked an invitation.",
        ],
      ],
      ["1 Oct 2026", ["Ayşe Yılmaz created this organisation."]],
    ]);
    expect(
      screen.getByRole("heading", { level: 1, name: "Activity" }),
    ).toBeInTheDocument();
  });

  it("ActivityPage_Entry_ShowsItsLocalTime", async () => {
    server.use(
      ...answer(MemberRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    const item = within(await screen.findByRole("listitem"));
    expect(item.getByText("09:40")).toHaveAttribute(
      "datetime",
      "2026-10-16T08:40:00.000Z",
    );
  });

  // 22:30 and 23:30 UTC on the 15th: before and after Dublin's midnight; 20:30 and 21:30 UTC: before and after Riyadh's.
  it.each([
    ["Europe/Dublin", "2026-10-15T23:30:00Z", "2026-10-15T22:30:00Z"],
    ["Asia/Riyadh", "2026-10-15T21:30:00Z", "2026-10-15T20:30:00Z"],
  ])(
    "ActivityPage_MidnightIn%s_PutsTheEntriesOnTwoDays",
    async (timeZone, after, before) => {
      server.use(
        ...answer(MemberRole.Owner, timeZone),
        auditLogAnswer({
          "": {
            items: [
              entry({ occurredAt: after }),
              entry({
                id: "0198f0a2-0000-7000-8000-0000000000e6",
                occurredAt: before,
                operation: AuditOperation.TenantCreated,
              }),
            ],
            nextCursor: null,
          },
        }),
      );

      renderInTenant(<ActivityPage />, { url });

      expect(await days()).toEqual([
        ["Today, 16 Oct 2026", ["Khalid Al-Harbi joined the organisation."]],
        ["Yesterday, 15 Oct 2026", ["Ayşe Yılmaz created this organisation."]],
      ]);
    },
  );

  it("ActivityPage_ShowOlderActivity_AppendsTheNextPageAndDisappearsAtTheEnd", async () => {
    const cursors: (string | null)[] = [];
    server.use(
      ...answer(MemberRole.Admin),
      auditLogAnswer(
        {
          "": { items: [invited, joined], nextCursor: "page-2" },
          "page-2": { items: [created], nextCursor: null },
        },
        cursors,
      ),
    );
    renderInTenant(<ActivityPage />, { url });

    await userEvent.click(
      await screen.findByRole("button", { name: "Show older activity" }),
    );

    expect(
      await findSentence("Ayşe Yılmaz created this organisation."),
    ).toBeInTheDocument();
    expect(
      getSentence("Khalid Al-Harbi joined the organisation."),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Show older activity" }),
    ).not.toBeInTheDocument();
    expect(cursors).toEqual([null, "page-2"]);
  });

  it("ActivityPage_LastPage_ShowsNoOlderButton", async () => {
    server.use(
      ...answer(MemberRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(await screen.findByRole("listitem")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Show older activity" }),
    ).not.toBeInTheDocument();
  });

  it("ActivityPage_CursorNoLongerValid_StartsFromTheNewestAndSaysSo", async () => {
    const cursors: (string | null)[] = [];
    server.use(...answer(MemberRole.Owner), refusesEveryCursor(cursors));
    renderInTenant(<ActivityPage />, { url });

    await userEvent.click(
      await screen.findByRole("button", { name: "Show older activity" }),
    );

    expect(
      await screen.findByText("Starting from the newest activity"),
    ).toBeInTheDocument();
    await vi.waitFor(() => expect(cursors).toEqual([null, "page-2", null]));
    expect(
      await findSentence("Khalid Al-Harbi joined the organisation."),
    ).toBeInTheDocument();
  });

  it("ActivityPage_NoEntries_SaysNoActivityYet", async () => {
    server.use(
      ...answer(MemberRole.Owner),
      auditLogAnswer({ "": { items: [], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(
      await screen.findByRole("heading", { name: "No activity yet" }),
    ).toBeInTheDocument();
  });

  it("ActivityPage_ListFails_ShowsTheReferenceAndTryAgainReadsAgain", async () => {
    server.use(...answer(MemberRole.Owner), failsOnce());
    renderInTenant(<ActivityPage />, { url });

    expect(
      await screen.findByRole("heading", { name: "Couldn’t load activity" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Ref 4bf92f3577b34da6a3ce929d0e0e4736/),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));

    expect(
      await findSentence("Khalid Al-Harbi joined the organisation."),
    ).toBeInTheDocument();
  });

  it("ActivityPage_Member_SaysPermissionDeniedAndCallsNothing", async () => {
    const cursors: (string | null)[] = [];
    server.use(
      ...answer(MemberRole.Member),
      auditLogAnswer({ "": { items: [], nextCursor: null } }, cursors),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(
      await screen.findByRole("heading", { name: "You can’t open Activity" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        `Your role in ${northwind.name} is Member. Owners and Admins can read Activity. Ask one of them if you need something from it.`,
      ),
    ).toBeInTheDocument();
    expect(cursors).toEqual([]);
  });

  it("ActivityPage_InTurkish_SaysTodayInTurkish", async () => {
    server.use(
      ...answer(MemberRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url, locale: "tr" });

    expect(
      await screen.findByRole("heading", {
        level: 2,
        name: "Bugün, 16 Eki 2026",
      }),
    ).toBeInTheDocument();
  });
});
