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
import { messages } from "@/i18n/messages";
import { POST } from "./route";

const getToken = vi.fn<() => Promise<string | null>>();
vi.mock("@clerk/nextjs/server", () => ({ auth: async () => ({ getToken }) }));
vi.mock("next-intl/server", () => ({
  getTranslations: async (namespace: "members" | "shell.roleNames") =>
    createTranslator({ locale: "en", messages: messages.en, namespace }),
}));

const api = "https://api.example.test";
const web = "https://web.example.test";
const tenantId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";
const server = setupServer();
const requests: Request[] = [];

const owner = { id: "r-owner", name: "owner", builtIn: true };
const lead = { id: "r-lead", name: "Support lead", builtIn: false };
const member = (index: number) => ({
  userId: `u-${index}`,
  name: `Member ${index}`,
  email: `member${index}@northwind.example`,
  role: lead,
  deleted: false,
  joinedAt: "2026-03-12T10:00:00Z",
});

/** ListMembers over `total` members, 100 to a page (the API's largest page). */
const members = (total: number) =>
  http.get(`${api}/v1/tenants/:tenantId/members`, ({ request }) => {
    const url = new URL(request.url);
    const page = Number(url.searchParams.get("page"));
    const pageSize = Number(url.searchParams.get("pageSize"));
    const start = (page - 1) * pageSize;
    const items = Array.from(
      { length: Math.max(0, Math.min(pageSize, total - start)) },
      (_, index) => member(start + index + 1),
    );
    return HttpResponse.json({ items, page, pageSize, total });
  });

const audit = (status = 204) =>
  http.post(`${api}/v1/tenants/:tenantId/members/export`, () =>
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
    new Request(`${web}/api/exports/tenants/${id}/members${query}`, {
      method: "POST",
    }),
    { params: Promise.resolve({ tenantId: id }) },
  );

const text = async (answer: Response) =>
  new TextDecoder("utf-8", { ignoreBOM: true }).decode(
    await answer.arrayBuffer(),
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

describe("Export members", () => {
  // The audit entry comes before the file (the maintainer's decision of 2026-11-06): no page is read before ExportMembers answers.
  it("ExportMembers_TwoPages_AuditsFirstThenWritesEveryMember", async () => {
    server.use(me(), tenant, audit(), members(150));

    const answer = await exportOf();
    const lines = (await text(answer)).split("\r\n");

    expect(answer.status).toBe(200);
    expect(answer.headers.get("Content-Type")).toBe("text/csv; charset=utf-8");
    expect(lines[0]).toBe("﻿Name,Email,Role,Joined");
    expect(lines[1]).toBe(
      "Member 1,member1@northwind.example,Support lead,2026-03-12",
    );
    expect(lines).toHaveLength(1 + 150 + 1);
    const paths = requests.map(
      (request) => `${request.method} ${new URL(request.url).pathname}`,
    );
    const auditAt = paths.indexOf(
      `POST /v1/tenants/${tenantId}/members/export`,
    );
    const firstPageAt = paths.indexOf(`GET /v1/tenants/${tenantId}/members`);
    expect(auditAt).toBeGreaterThanOrEqual(0);
    expect(auditAt).toBeLessThan(firstPageAt);
    expect(
      requests
        .filter((request) => request.method === "GET")
        .map((request) => new URL(request.url).searchParams.get("page"))
        .filter(Boolean),
    ).toEqual(["1", "2"]);
  });

  // A refused permission answers the API's problem, and no page is read (F4).
  it("ExportMembers_AuditRefused_AnswersTheProblemAndReadsNoPage", async () => {
    server.use(me(), tenant, audit(403), members(5));

    const answer = await exportOf();

    expect(answer.status).toBe(403);
    expect(await answer.json()).toMatchObject({ code: "auth.forbidden" });
    expect(
      requests.some((request) =>
        new URL(request.url).pathname.endsWith("/members"),
      ),
    ).toBe(false);
  });

  // The file holds the list as the screen filters and sorts it.
  it("ExportMembers_SearchRoleAndSort_PassesThemToEveryPage", async () => {
    server.use(me(), tenant, audit(), members(3));

    await text(await exportOf("?q=omar&roleId=r-lead&sort=-name"));

    const [page] = requests.filter((request) =>
      new URL(request.url).pathname.endsWith("/members"),
    );
    const params = new URL(String(page?.url)).searchParams;
    expect(params.get("q")).toBe("omar");
    expect(params.get("roleId")).toBe("r-lead");
    expect(params.get("sort")).toBe("-name");
    expect(params.get("pageSize")).toBe("100");
  });

  // A page that fails midway cuts the answer, so the browser never keeps half a file (the maintainer's decision of 2026-11-07).
  it("ExportMembers_PageFailsMidway_CutsTheAnswer", async () => {
    server.use(
      me(),
      tenant,
      audit(),
      http.get(
        `${api}/v1/tenants/:tenantId/members`,
        () =>
          HttpResponse.json({
            items: [member(1)],
            page: 1,
            pageSize: 1,
            total: 2,
          }),
        { once: true },
      ),
      http.get(`${api}/v1/tenants/:tenantId/members`, () =>
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

  // The file's name has the day in the person's time zone (D12): 23:30 in UTC is already the next day in Istanbul.
  it("ExportMembers_LateUtcEvening_NamesTheFileWithTheNextDayInIstanbul", async () => {
    vi.setSystemTime(new Date("2026-11-07T23:30:00Z"));
    server.use(me("Europe/Istanbul"), tenant, audit(), members(1));

    const answer = await exportOf();

    expect(answer.headers.get("Content-Disposition")).toBe(
      'attachment; filename="northwind-members-2026-11-08.csv"',
    );
  });

  // A person without a time zone of their own takes the browser's, which the screen sends (F18).
  it("ExportMembers_NoTimeZoneOfTheirOwn_TakesTheBrowsersZone", async () => {
    vi.setSystemTime(new Date("2026-11-07T23:30:00Z"));
    server.use(me(null), tenant, audit(), members(1));

    const answer = await exportOf("?timeZone=Europe/Istanbul");

    expect(answer.headers.get("Content-Disposition")).toBe(
      'attachment; filename="northwind-members-2026-11-08.csv"',
    );
  });

  // A built-in role in the reader's language, a deleted user as the list names it, and a formula start escaped (OWASP).
  it("ExportMembers_Rows_WriteTheRoleNameDeletedUserAndEscapeFormulas", async () => {
    server.use(
      me(),
      tenant,
      audit(),
      http.get(`${api}/v1/tenants/:tenantId/members`, () =>
        HttpResponse.json({
          items: [
            { ...member(1), name: "=HYPERLINK(1)", role: owner },
            { ...member(2), name: null, email: null, deleted: true },
          ],
          page: 1,
          pageSize: 100,
          total: 2,
        }),
      ),
    );

    const lines = (await text(await exportOf())).split("\r\n");

    expect(lines[1]).toBe(
      "'=HYPERLINK(1),member1@northwind.example,Owner,2026-03-12",
    );
    expect(lines[2]).toBe("Deleted user,,Support lead,2026-03-12");
  });

  // Next decodes the route's segment, so an id with a separator would reach the API path decoded: anything but a UUID is
  // refused before any call, as the BFF refuses an encoded separator (F6).
  it.each(["../me", "x/../../platform", "not-an-id"])(
    "ExportMembers_TenantIdIsNoUuid_%s_Answers404AndCallsNothing",
    async (id) => {
      const answer = await exportOf("", id);

      expect(answer.status).toBe(404);
      expect(requests).toEqual([]);
    },
  );
});
