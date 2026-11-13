import {
  AuditActorResponseKind,
  AuditOperation,
  type AuditRetentionResponse,
  BuiltInRole,
  ErrorCode,
  TenantPurgeWarnedDetailsResponseOperation,
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
import {
  auditLogAnswer,
  detailsOf,
  entry,
  keptForAYear,
  retentionAnswer,
} from "../../../test/activity";
import { savedFiles } from "../../../test/download";
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

/**
 * The tenant's answers for a viewer of the role, whose time zone is the one given, and the log's period. The first handler that
 * matches answers.
 */
const answer = (
  role: BuiltInRole,
  timeZone = "Europe/Dublin",
  retention: AuditRetentionResponse = keptForAYear,
) => [
  http.get("*/v1/me", () => HttpResponse.json({ ...meOf(role), timeZone })),
  ...tenantAnswers(role),
  retentionAnswer(retention),
];

/** The paragraph of an entry's sentence: the names inside it are elements of their own. */
const isSentence = (text: string) => (_: string, element: Element | null) =>
  element?.tagName === "P" && element.textContent === text;
const findSentence = (text: string) => screen.findByText(isSentence(text));
const getSentence = (text: string) => screen.getByText(isSentence(text));

/** The first page answers with a next cursor; a request with a cursor is refused: 400 paging.cursor_invalid, 401, or a server error. */
const refusesEveryCursor = (cursors: (string | null)[], status = 400) =>
  http.get("*/v1/tenants/:tenantId/audit-log", ({ request }) => {
    const cursor = new URL(request.url).searchParams.get("cursor");
    cursors.push(cursor);
    return cursor
      ? problem(
          status,
          {
            400: ErrorCode.PagingCursorInvalid,
            401: ErrorCode.AuthUnauthenticated,
          }[status] ?? ErrorCode.ServerError,
        )
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
      ...answer(BuiltInRole.Owner),
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
      screen.getByRole("heading", { level: 1, name: "Activity log" }),
    ).toBeInTheDocument();
    // No Freshness under the title (F59): the frame's description sentence stands there (12.01).
    expect(
      screen.getByText(
        "Changes to members, invitations, roles and the organisation.",
      ),
    ).toBeVisible();
    expect(screen.queryByText(/Updated/)).not.toBeInTheDocument();
  });

  it("ActivityPage_Entry_ShowsItsLocalTime", async () => {
    server.use(
      ...answer(BuiltInRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    const item = within(await screen.findByRole("listitem"));
    expect(item.getByText("09:40")).toHaveAttribute(
      "datetime",
      "2026-10-16T08:40:00.000Z",
    );
  });

  // 02:00 UTC on 10 Oct is still 9 Oct in New York: the page gives the sentence the viewer's zone, not the runtime's.
  it("ActivityPage_PurgeWarned_NamesTheDayInTheViewersZone", async () => {
    server.use(
      ...answer(BuiltInRole.Owner, "America/New_York"),
      auditLogAnswer({
        "": {
          items: [
            entry({
              operation: AuditOperation.TenantPurgeWarned,
              details: {
                operation:
                  TenantPurgeWarnedDetailsResponseOperation.TenantPurgeWarned,
                purgesAt: "2026-10-10T02:00:00Z",
              },
            }),
          ],
          nextCursor: null,
        },
      }),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(
      await screen.findByText(/will be deleted on 9 Oct 2026\./),
    ).toBeInTheDocument();
  });

  // 22:30 and 23:30 UTC on the 15th: before and after Dublin's midnight; 20:30 and 21:30 UTC: before and after Riyadh's.
  it.each([
    ["Europe/Dublin", "2026-10-15T23:30:00Z", "2026-10-15T22:30:00Z"],
    ["Asia/Riyadh", "2026-10-15T21:30:00Z", "2026-10-15T20:30:00Z"],
  ])(
    "ActivityPage_MidnightIn%s_PutsTheEntriesOnTwoDays",
    async (timeZone, after, before) => {
      server.use(
        ...answer(BuiltInRole.Owner, timeZone),
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
      ...answer(BuiltInRole.Admin),
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
      ...answer(BuiltInRole.Owner),
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
    server.use(...answer(BuiltInRole.Owner), refusesEveryCursor(cursors));
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
      ...answer(BuiltInRole.Owner),
      auditLogAnswer({ "": { items: [], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(
      await screen.findByRole("heading", { name: "No activity yet" }),
    ).toBeInTheDocument();
  });

  it("ActivityPage_ListFails_ShowsTheReferenceAndTryAgainReadsAgain", async () => {
    server.use(...answer(BuiltInRole.Owner), failsOnce());
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
      ...answer(BuiltInRole.Member),
      auditLogAnswer({ "": { items: [], nextCursor: null } }, cursors),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(
      await screen.findByRole("heading", { name: "You can’t open this page" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        `Your role in ${northwind.name} doesn’t include Activity log. Ask an Owner or Admin if you need it.`,
      ),
    ).toBeInTheDocument();
    expect(cursors).toEqual([]);
  });

  // 2.15: the search-x tile over the title, and the way back to Home.
  it("ActivityPage_Member_ShowsTheTileAndBackToHome", async () => {
    server.use(
      ...answer(BuiltInRole.Member),
      auditLogAnswer({ "": { items: [], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    const back = await screen.findByRole("button", { name: "Back to Home" });
    expect(back).toHaveAttribute("href", `/${northwind.slug}`);
    expect(document.querySelector("svg.lucide-search-x")).toBeInTheDocument();
  });

  it("ActivityPage_InTurkish_SaysTodayInTurkish", async () => {
    server.use(
      ...answer(BuiltInRole.Owner),
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

  // A failed older page says so under the entries, with Try again, and keeps them (12.06).
  it("ActivityPage_OlderPageFails_SaysSoUnderTheEntriesAndTriesAgain", async () => {
    const cursors: (string | null)[] = [];
    server.use(...answer(BuiltInRole.Owner), refusesEveryCursor(cursors, 500));
    renderInTenant(<ActivityPage />, { url });

    await userEvent.click(
      await screen.findByRole("button", { name: "Show older activity" }),
    );

    expect(
      await screen.findByText("Couldn’t load older activity."),
    ).toBeVisible();
    expect(
      getSentence("Khalid Al-Harbi joined the organisation."),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Show older activity" }),
    ).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    await vi.waitFor(() => expect(cursors).toHaveLength(3));
  });

  // While the older page comes, a row says so in the button's place, and a live region says how many came (12.02, F9).
  it("ActivityPage_ShowOlderActivity_SaysItLoadsAndHowManyCame", async () => {
    let release = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      ...answer(BuiltInRole.Owner),
      http.get("*/v1/tenants/:tenantId/audit-log", async ({ request }) => {
        const cursor = new URL(request.url).searchParams.get("cursor");
        if (!cursor)
          return HttpResponse.json({
            items: [invited, joined],
            nextCursor: "page-2",
          });
        await held;
        return HttpResponse.json({ items: [created], nextCursor: null });
      }),
    );
    renderInTenant(<ActivityPage />, { url });

    await userEvent.click(
      await screen.findByRole("button", { name: "Show older activity" }),
    );

    expect(await screen.findByText("Loading older activity…")).toBeVisible();
    release();
    expect(await screen.findByText("1 more entry loaded.")).toBeInTheDocument();
  });

  it("ActivityPage_OlderPageSignedOut_GoesToSignIn", async () => {
    const assign = vi.fn();
    vi.stubGlobal("location", { ...window.location, assign });
    server.use(...answer(BuiltInRole.Owner), refusesEveryCursor([], 401));
    renderInTenant(<ActivityPage />, { url });

    await userEvent.click(
      await screen.findByRole("button", { name: "Show older activity" }),
    );

    await vi.waitFor(() =>
      expect(assign).toHaveBeenCalledWith(
        expect.stringMatching(/^\/sign-in\?reason=session-ended&redirect_url=/),
      ),
    );
    vi.unstubAllGlobals();
  });
});

/** A hold of 30 days runs to 1 Dec 2026 in Dublin, and entries older than the new period of 90 days exist (12.10). */
const shortened: AuditRetentionResponse = {
  days: 90,
  shortening: { days: 90, goesAt: "2026-12-01T10:00:00Z" },
};

/** The Banner whose title is the text given. */
const bannerOf = async (title: string) =>
  (await screen.findByText(title)).closest('[role="status"]') as HTMLElement;

/** The period's sentence under the description: a paragraph of its own. */
const periodSentence = (text: string) =>
  screen.findByText(text, { selector: "p" });

describe("The Activity log's period", () => {
  it("ActivityPage_APeriod_SaysHowLongEntriesAreKept", async () => {
    server.use(
      ...answer(BuiltInRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(
      await periodSentence("Entries are kept for 365 days."),
    ).toBeVisible();
  });

  it("ActivityPage_NoLimit_SaysEntriesAreKeptWithNoTimeLimit", async () => {
    server.use(
      ...answer(BuiltInRole.Owner, "Europe/Dublin", {
        days: null,
        shortening: null,
      }),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(
      await periodSentence("Entries are kept with no time limit."),
    ).toBeVisible();
  });

  it.each([
    ["tr", 365, "Kayıtlar 365 gün saklanır."],
    ["tr", null, "Kayıtlar süre sınırı olmadan saklanır."],
    ["ar", 30, "تُحفظ الإدخالات لمدة 30 يومًا."],
    ["ar", 90, "تُحفظ الإدخالات لمدة 90 يومًا."],
    ["ar", 100, "تُحفظ الإدخالات لمدة 100 يوم."],
    ["ar", 365, "تُحفظ الإدخالات لمدة 365 يومًا."],
    ["ar", null, "تُحفظ الإدخالات دون حد زمني."],
  ] as const)(
    "ActivityPage_PeriodIn%s_%s_SaysItInTheLanguagesPlural",
    async (locale, days, text) => {
      server.use(
        ...answer(BuiltInRole.Owner, "Europe/Dublin", {
          days,
          shortening: null,
        }),
        auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
      );

      renderInTenant(<ActivityPage />, { url, locale });

      expect(await periodSentence(text)).toBeVisible();
    },
  );

  it("ActivityPage_NoShortening_ShowsNoBanner", async () => {
    server.use(
      ...answer(BuiltInRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    await periodSentence("Entries are kept for 365 days.");
    expect(screen.queryByText(/go on/)).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Export CSV" }),
    ).not.toBeInTheDocument();
  });

  // 12.10: the Banner names the new period and the hold's end, in the viewer's zone, and offers the export.
  it("ActivityPage_AShortening_ShowsTheBannerWithItsDateAndExport", async () => {
    server.use(
      ...answer(BuiltInRole.Owner, "Europe/Dublin", shortened),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    const banner = await bannerOf(
      "Entries older than 90 days go on 1 Dec 2026.",
    );
    expect(banner).toHaveTextContent(
      "The Activity log now keeps entries for 90 days. To keep a copy of the older ones, export them before then.",
    );
    expect(await periodSentence("Entries are kept for 90 days.")).toBeVisible();
    await userEvent.click(
      within(banner).getByRole("button", { name: "Export CSV" }),
    );
    expect(
      await screen.findByRole("alertdialog", {
        name: "Export the activity log",
      }),
    ).toBeInTheDocument();
  });

  it("ActivityPage_AShorteningEndingAfterMidnightInTheViewersZone_NamesTheViewersDay", async () => {
    server.use(
      // 30 Nov at 23:30 in UTC is 1 Dec at 08:30 in Tokyo.
      ...answer(BuiltInRole.Owner, "Asia/Tokyo", {
        days: 90,
        shortening: { days: 90, goesAt: "2026-11-30T23:30:00Z" },
      }),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(
      await bannerOf("Entries older than 90 days go on 1 Dec 2026."),
    ).toBeInTheDocument();
  });

  it.each([
    [
      "tr",
      "90 günden eski kayıtlar 1 Ara 2026 tarihinde silinir.",
      "Etkinlik kaydı artık kayıtları 90 gün saklıyor. Eskilerin bir kopyasını tutmak için o tarihten önce dışa aktarın.",
      "CSV olarak dışa aktar",
    ],
    [
      "ar",
      "تُحذف الإدخالات الأقدم من 90 يومًا في 1 ديسمبر 2026.",
      "يحتفظ سجل النشاط الآن بالإدخالات لمدة 90 يومًا. للاحتفاظ بنسخة من الإدخالات الأقدم، صدّرها قبل ذلك التاريخ.",
      "تصدير CSV",
    ],
  ] as const)(
    "ActivityPage_AShorteningIn%s_ShowsTheBannersTexts",
    async (locale, title, text, action) => {
      server.use(
        ...answer(BuiltInRole.Owner, "Europe/Dublin", shortened),
        auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
      );

      renderInTenant(<ActivityPage />, { url, locale });

      const banner = await bannerOf(title);
      expect(banner).toHaveTextContent(text);
      expect(
        within(banner).getByRole("button", { name: action }),
      ).toBeInTheDocument();
    },
  );

  it("ActivityPage_ArabicBannerOf100Days_UsesTheFormOfOther", async () => {
    server.use(
      ...answer(BuiltInRole.Owner, "Europe/Dublin", {
        days: 100,
        shortening: { days: 100, goesAt: "2026-12-01T10:00:00Z" },
      }),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url, locale: "ar" });

    expect(
      await bannerOf("تُحذف الإدخالات الأقدم من 100 يوم في 1 ديسمبر 2026."),
    ).toBeInTheDocument();
  });

  // No frame draws a failed period: the page shows the log without the sentence and the Banner.
  it("ActivityPage_PeriodFails_ShowsTheLogWithoutPeriodOrBanner", async () => {
    server.use(
      http.get("*/v1/tenants/:tenantId/audit-log/retention", () =>
        problem(500, ErrorCode.ServerError),
      ),
      ...answer(BuiltInRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    expect(
      await findSentence("Khalid Al-Harbi joined the organisation."),
    ).toBeInTheDocument();
    expect(screen.queryByText(/Entries are kept/)).not.toBeInTheDocument();
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  // The export's permission is the log's own (tenant.audit.read): a viewer without it sees neither the Banner nor its Export.
  it("ActivityPage_ViewerWithoutTheExportsPermission_ReadsNoPeriodAndShowsNoExport", async () => {
    const tenants: string[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Member),
      retentionAnswer(shortened, tenants),
      auditLogAnswer({ "": { items: [], nextCursor: null } }),
    );

    renderInTenant(<ActivityPage />, { url });

    await screen.findByRole("heading", { name: "You can’t open this page" });
    expect(
      screen.queryByRole("button", { name: "Export CSV" }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(tenants).toEqual([]);
  });
});

const exportPath = `*/exports/tenants/${northwind.id}/audit-log`;
const fileName = "northwind-activity-log-2026-10-16.csv";

/** The log's CSV file of the web's server with `records` entries, and the query of each request. */
const exportAnswer = (records: number, requests: URLSearchParams[] = []) =>
  http.post(exportPath, ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.text(
      `\uFEFFTime (UTC),Who,What\r\n${"2026-10-16T08:40:00.000Z,Khalid,joined\r\n".repeat(records)}`,
      {
        headers: {
          "Content-Type": "text/csv; charset=utf-8",
          "Content-Disposition": `attachment; filename="${fileName}"`,
        },
      },
    );
  });

const openExport = async () => {
  await userEvent.click(
    await screen.findByRole("button", { name: "More actions" }),
  );
  await userEvent.click(
    await screen.findByRole("menuitem", { name: "Export CSV" }),
  );
  return screen.findByRole("alertdialog", { name: "Export the activity log" });
};

describe("Export the activity log", () => {
  // The dialog says what the file holds (12.08); the server names the file, and the toast names it.
  it("ExportActivityLog_Confirm_SavesTheServersFileAndSaysSo", async () => {
    const requests: URLSearchParams[] = [];
    const saved = savedFiles();
    server.use(
      ...answer(BuiltInRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
      exportAnswer(2, requests),
    );
    renderInTenant(<ActivityPage />, { url });

    const dialog = await openExport();
    expect(dialog).toHaveTextContent(
      "A CSV file with every entry of Northwind Traders, newest first, up to 100,000 entries. Columns: Time (UTC), Who, What.",
    );
    expect(dialog).toHaveTextContent(
      "Times are in ISO 8601 UTC, for example 2026-11-01T14:32:00Z. Entries of deleted people say “Deleted user”.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Export CSV" }),
    );

    await expectVisibleToast("Activity log exported");
    expect(
      screen.getByText(`${fileName} is in your downloads.`),
    ).toBeInTheDocument();
    expect(saved).toEqual([fileName]);
    expect(requests[0]?.get("timeZone")).toBeTruthy();
  });

  // The file stops at its cap (the maintainer's decision of 2026-11-07): a file that holds the cap says so.
  it("ExportActivityLog_FileAtTheCap_SaysItHoldsTheNewestEntries", async () => {
    savedFiles();
    server.use(
      ...answer(BuiltInRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
      exportAnswer(100_000),
    );
    renderInTenant(<ActivityPage />, { url });

    await userEvent.click(
      within(await openExport()).getByRole("button", { name: "Export CSV" }),
    );

    await expectVisibleToast("Activity log exported");
    expect(
      screen.getByText("The file holds the newest 100,000 entries."),
    ).toBeInTheDocument();
  });

  // A refusal keeps the dialog open with the problem's own text (F4), and saves nothing.
  it("ExportActivityLog_Refused_ShowsTheProblemAndSavesNothing", async () => {
    const saved = savedFiles();
    server.use(
      ...answer(BuiltInRole.Owner),
      auditLogAnswer({ "": { items: [joined], nextCursor: null } }),
      http.post(exportPath, () => problem(403, ErrorCode.AuthForbidden)),
    );
    renderInTenant(<ActivityPage />, { url });

    const dialog = await openExport();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Export CSV" }),
    );

    expect(
      await within(dialog).findByText(/^Couldn’t create the file\./),
    ).toBeVisible();
    expect(saved).toEqual([]);
  });
});
