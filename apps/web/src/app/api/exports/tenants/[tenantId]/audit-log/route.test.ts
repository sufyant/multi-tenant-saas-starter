import { AuditActorResponseKind } from "@repo/api-client";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import { createTranslator } from "next-intl";
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
import { activityFileCap } from "@/features/activity/file-cap";
import { messages } from "@/i18n/messages";
import { entry } from "../../../../../../../test/activity";
import { POST } from "./route";

const getToken = vi.fn<() => Promise<string | null>>();
vi.mock("@clerk/nextjs/server", () => ({ auth: async () => ({ getToken }) }));
vi.mock("next-intl/server", () => ({
  getTranslations: async () =>
    createTranslator({ locale: "en", messages: messages.en }),
}));

const api = "https://api.example.test";
const web = "https://web.example.test";
const tenantId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";
const server = setupServer();
const requests: Request[] = [];

/** One entry of the log: Khalid joined at a second of 16 Oct 2026. */
const joined = (index: number) =>
  entry({
    id: `0198f0a2-0000-7000-8000-${String(index).padStart(12, "0")}`,
    occurredAt: "2026-10-16T08:40:00Z",
  });

/** ListAuditEntries over `total` entries, `limit` to a page, by a cursor that is the next page's number. */
const log = (total: number) =>
  http.get(`${api}/v1/tenants/:tenantId/audit-log`, ({ request }) => {
    const url = new URL(request.url);
    const limit = Number(url.searchParams.get("limit"));
    const page = Number(url.searchParams.get("cursor") ?? "0");
    const start = page * limit;
    const items = Array.from(
      { length: Math.max(0, Math.min(limit, total - start)) },
      (_, index) => joined(start + index),
    );
    return HttpResponse.json({
      items,
      nextCursor: start + limit < total ? String(page + 1) : null,
    });
  });

const audit = (status = 204) =>
  http.post(`${api}/v1/tenants/:tenantId/audit-log/export`, () =>
    status === 204
      ? new HttpResponse(null, { status })
      : HttpResponse.json(
          { status, code: "auth.forbidden", traceId: "00-trace" },
          {
            status,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
  );

const me = (timeZone: string | null = "Europe/Dublin") =>
  http.get(`${api}/v1/me`, () =>
    HttpResponse.json({
      id: "u-1",
      email: "ayse@northwind.example",
      name: "Ayşe Demir",
      isSystemAdmin: false,
      language: "en",
      timeZone,
    }),
  );

const tenant = http.get(`${api}/v1/tenants/:tenantId`, () =>
  HttpResponse.json({
    id: tenantId,
    name: "Northwind Traders",
    slug: "northwind",
  }),
);

const exportOf = (query = "", id = tenantId) =>
  POST(
    new Request(`${web}/api/exports/tenants/${id}/audit-log${query}`, {
      method: "POST",
    }),
    { params: Promise.resolve({ tenantId: id }) },
  );

const text = async (answer: Response) =>
  new TextDecoder("utf-8", { ignoreBOM: true }).decode(
    await answer.arrayBuffer(),
  );

const pagesRead = () =>
  requests.filter((request) =>
    new URL(request.url).pathname.endsWith("/audit-log"),
  );

beforeAll(() => {
  vi.stubEnv("API_URL", api);
  vi.stubEnv("CLERK_PUBLISHABLE_KEY", "pk_test_x");
  vi.stubEnv("CLERK_SECRET_KEY", "sk_test_x");
  vi.stubEnv("APP_ENVIRONMENT", "local");
  vi.stubEnv("WEB_APP_URL", "http://localhost:3000");
  server.listen({ onUnhandledFrame: "error" });
  server.events.on("request:start", ({ request }) => {
    requests.push(request.clone());
  });
});
beforeEach(() => {
  getToken.mockResolvedValue("session-token");
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-11-07T12:00:00Z"));
});
afterEach(() => {
  server.resetHandlers();
  requests.length = 0;
  vi.useRealTimers();
});
afterAll(() => server.close());

describe("Export the activity log", () => {
  // The audit entry comes before the file (the maintainer's decision of 2026-11-06): no page is read before ExportAuditLog answers.
  it("ExportAuditLog_TwoPages_AuditsFirstThenWritesEveryEntry", async () => {
    server.use(me(), tenant, audit(), log(150));

    const answer = await exportOf();
    const lines = (await text(answer)).split("\r\n");

    expect(answer.status).toBe(200);
    expect(answer.headers.get("Content-Type")).toBe("text/csv; charset=utf-8");
    expect(lines[0]).toBe("﻿Time (UTC),Who,What");
    expect(lines[1]).toBe(
      "2026-10-16T08:40:00Z,Ayşe Yılmaz,Khalid Al-Harbi joined the organisation.",
    );
    expect(lines).toHaveLength(1 + 150 + 1);
    const paths = requests.map(
      (request) => `${request.method} ${new URL(request.url).pathname}`,
    );
    expect(
      paths.indexOf(`POST /v1/tenants/${tenantId}/audit-log/export`),
    ).toBeLessThan(paths.indexOf(`GET /v1/tenants/${tenantId}/audit-log`));
    expect(
      pagesRead().map((request) => [
        new URL(request.url).searchParams.get("cursor"),
        new URL(request.url).searchParams.get("limit"),
      ]),
    ).toEqual([
      [null, "100"],
      ["1", "100"],
    ]);
  });

  // A refused permission answers the API's problem, and no page is read (F4).
  it("ExportAuditLog_AuditRefused_AnswersTheProblemAndReadsNoPage", async () => {
    server.use(me(), tenant, audit(403), log(5));

    const answer = await exportOf();

    expect(answer.status).toBe(403);
    expect(await answer.json()).toMatchObject({ code: "auth.forbidden" });
    expect(pagesRead()).toEqual([]);
  });

  // The cap is one constant (the maintainer's decision of 2026-11-07).
  it("ActivityFileCap_IsTheNewest100000Entries", () => {
    expect(activityFileCap).toBe(100_000);
  });

  // A log longer than the cap writes its newest entries up to the cap, and reads no page after it.
  it("ExportAuditLog_LogOverTheCap_WritesTheNewestEntriesUpToTheCap", async () => {
    server.use(me(), tenant, audit(), log(activityFileCap + 250));

    const lines = (await text(await exportOf())).split("\r\n");

    expect(lines).toHaveLength(1 + activityFileCap + 1);
    expect(pagesRead()).toHaveLength(activityFileCap / 100);
  }, 60_000);

  // The rows: "Deleted user" for a deleted person (12.08), the platform by its name, and a formula start escaped (OWASP).
  it("ExportAuditLog_Rows_WriteDeletedUserThePlatformAndEscapeFormulas", async () => {
    server.use(
      me(),
      tenant,
      audit(),
      http.get(`${api}/v1/tenants/:tenantId/audit-log`, () =>
        HttpResponse.json({
          items: [
            entry({
              actor: {
                kind: AuditActorResponseKind.User,
                userId: "u-9",
                displayName: null,
                deleted: true,
              },
            }),
            entry({
              actor: {
                kind: AuditActorResponseKind.Platform,
                userId: null,
                displayName: null,
                deleted: null,
              },
            }),
            entry({
              actor: {
                kind: AuditActorResponseKind.User,
                userId: "u-8",
                displayName: "=HYPERLINK(1)",
                deleted: false,
              },
            }),
          ],
          nextCursor: null,
        }),
      ),
    );

    const lines = (await text(await exportOf())).split("\r\n");

    expect(lines.slice(1, 4)).toEqual([
      "2026-10-16T13:32:00Z,Deleted user,Khalid Al-Harbi joined the organisation.",
      "2026-10-16T13:32:00Z,The platform,Khalid Al-Harbi joined the organisation.",
      "2026-10-16T13:32:00Z,'=HYPERLINK(1),Khalid Al-Harbi joined the organisation.",
    ]);
  });

  // A page that fails midway cuts the answer, so the browser never keeps half a file (the maintainer's decision of 2026-11-07).
  it("ExportAuditLog_PageFailsMidway_CutsTheAnswer", async () => {
    server.use(
      me(),
      tenant,
      audit(),
      http.get(
        `${api}/v1/tenants/:tenantId/audit-log`,
        () => HttpResponse.json({ items: [joined(1)], nextCursor: "1" }),
        { once: true },
      ),
      http.get(`${api}/v1/tenants/:tenantId/audit-log`, () =>
        HttpResponse.json(
          { status: 500, code: "server.error", traceId: "00-trace" },
          { status: 500 },
        ),
      ),
    );

    const answer = await exportOf();

    expect(answer.status).toBe(200);
    await expect(text(answer)).rejects.toThrow();
  });

  // The file's name has the day in the person's time zone: 23:30 in UTC is already the next day in Istanbul.
  it("ExportAuditLog_LateUtcEvening_NamesTheFileWithTheNextDayInIstanbul", async () => {
    vi.setSystemTime(new Date("2026-11-07T23:30:00Z"));
    server.use(me("Europe/Istanbul"), tenant, audit(), log(1));

    const answer = await exportOf();

    expect(answer.headers.get("Content-Disposition")).toBe(
      'attachment; filename="northwind-activity-log-2026-11-08.csv"',
    );
  });

  it("ExportAuditLog_NoTimeZoneOfTheirOwn_TakesTheBrowsersZone", async () => {
    vi.setSystemTime(new Date("2026-11-07T23:30:00Z"));
    server.use(me(null), tenant, audit(), log(1));

    const answer = await exportOf("?timeZone=Europe/Istanbul");

    expect(answer.headers.get("Content-Disposition")).toBe(
      'attachment; filename="northwind-activity-log-2026-11-08.csv"',
    );
  });

  // Anything but a UUID is refused before any call, as the BFF refuses an encoded separator (F6).
  it.each(["../me", "x/../../platform", "not-an-id"])(
    "ExportAuditLog_TenantIdIsNoUuid_%s_Answers404AndCallsNothing",
    async (id) => {
      const answer = await exportOf("", id);

      expect(answer.status).toBe(404);
      expect(requests).toEqual([]);
    },
  );

  // A zone the browser sends is checked before the audit call, so no audit entry says that a file left (F4).
  it("ExportAuditLog_InvalidTimeZone_Answers400AndWritesNoAuditEntry", async () => {
    server.use(me(null), tenant, audit(), log(1));

    const answer = await exportOf("?timeZone=Mars/Olympus");

    expect(answer.status).toBe(400);
    expect(await answer.json()).toMatchObject({ code: "request.invalid" });
    expect(requests.some((request) => request.method === "POST")).toBe(false);
  });
});
