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
import { GET, PATCH, POST } from "./route";

const getToken = vi.fn<() => Promise<string | null>>();
vi.mock("@clerk/nextjs/server", () => ({ auth: async () => ({ getToken }) }));

const api = "https://api.example.test";
const web = "https://web.example.test";
const tenantId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";
const server = setupServer();
const requests: Request[] = [];

beforeAll(() => {
  vi.stubEnv("API_URL", api);
  vi.stubEnv("CLERK_PUBLISHABLE_KEY", "pk_test_x");
  vi.stubEnv("CLERK_SECRET_KEY", "sk_test_x");
  server.listen({ onUnhandledFrame: "error" });
  server.events.on("request:start", ({ request }) => {
    requests.push(request.clone());
  });
});
beforeEach(() => {
  getToken.mockResolvedValue("session-token");
});
afterEach(() => {
  server.resetHandlers();
  requests.length = 0;
});
afterAll(() => server.close());

const tenant = {
  id: tenantId,
  name: "Atlas Logistics",
  slug: "atlas",
  status: "active",
  suspensionReason: null,
  closedAt: null,
};

describe("BFF", () => {
  it("GetMe_WithASession_AddsTheBearerToken", async () => {
    server.use(
      http.get(`${api}/v1/me`, () => HttpResponse.json({ id: tenantId })),
    );

    const answer = await GET(new Request(`${web}/api/v1/me`));

    expect(answer.status).toBe(200);
    expect(requests[0]?.headers.get("Authorization")).toBe(
      "Bearer session-token",
    );
  });

  it("CreateTenant_WithIdempotencyKey_ForwardsTheKeyAndTheBody", async () => {
    server.use(
      http.post(`${api}/v1/platform/tenants`, () =>
        HttpResponse.json(tenant, { status: 201 }),
      ),
    );

    await POST(
      new Request(`${web}/api/v1/platform/tenants`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": "key-1",
        },
        body: JSON.stringify({
          name: "Atlas Logistics",
          slug: "atlas",
          ownerEmail: "owner@atlas.example",
        }),
      }),
    );

    expect(requests[0]?.headers.get("Idempotency-Key")).toBe("key-1");
    expect(await requests[0]?.json()).toEqual({
      name: "Atlas Logistics",
      slug: "atlas",
      ownerEmail: "owner@atlas.example",
    });
  });

  it("ChangeMemberRole_Conflict_ReturnsTheBodyAndStatusUnchanged", async () => {
    const conflict = {
      status: 409,
      code: "member.last_owner",
      traceId: "00-trace",
    };
    server.use(
      http.patch(`${api}/v1/tenants/:tenantId/members/:userId`, () =>
        HttpResponse.json(conflict, {
          status: 409,
          headers: { "Content-Type": "application/problem+json" },
        }),
      ),
    );

    const answer = await PATCH(
      new Request(`${web}/api/v1/tenants/${tenantId}/members/${tenantId}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ role: "member" }),
      }),
    );

    expect(answer.status).toBe(409);
    expect(answer.headers.get("Content-Type")).toBe("application/problem+json");
    expect(await answer.json()).toEqual(conflict);
  });

  it("GetMe_AcceptAndACookie_PassesAcceptButNeverTheCookie", async () => {
    server.use(
      http.get(`${api}/v1/me`, () => HttpResponse.json({ id: tenantId })),
    );

    await GET(
      new Request(`${web}/api/v1/me`, {
        headers: { Accept: "application/json", Cookie: "__session=secret" },
      }),
    );

    expect(requests[0]?.headers.get("Accept")).toBe("application/json");
    expect(requests[0]?.headers.has("Cookie")).toBe(false);
  });

  it("GetMe_ApiUnreachable_Answers502WithAProblem", async () => {
    server.use(http.get(`${api}/v1/me`, () => HttpResponse.error()));

    const answer = await GET(new Request(`${web}/api/v1/me`));

    expect(answer.status).toBe(502);
    expect((await answer.json()).code).toBe("server.error");
  });

  it("GetMe_WithoutASession_Answers401AndCallsNothing", async () => {
    getToken.mockResolvedValue(null);

    const answer = await GET(new Request(`${web}/api/v1/me`));

    expect(answer.status).toBe(401);
    expect(requests).toEqual([]);
  });

  it("CreateTenant_ReplayedAnswer_PassesTheDeclaredHeaderOnly", async () => {
    server.use(
      http.post(`${api}/v1/platform/tenants`, () =>
        HttpResponse.json(tenant, {
          status: 201,
          headers: {
            "Idempotent-Replayed": "true",
            "X-Powered-By": "api",
            Connection: "close",
          },
        }),
      ),
    );

    const answer = await POST(
      new Request(`${web}/api/v1/platform/tenants`, {
        method: "POST",
        headers: { "Idempotency-Key": "key-1" },
        body: "{}",
      }),
    );

    expect(answer.headers.get("Idempotent-Replayed")).toBe("true");
    expect(answer.headers.has("X-Powered-By")).toBe(false);
    expect(answer.headers.has("Connection")).toBe(false);
  });

  it("CreateTenant_Location_PointsAtTheBff", async () => {
    server.use(
      http.post(`${api}/v1/platform/tenants`, () =>
        HttpResponse.json(tenant, {
          status: 201,
          headers: { Location: `/v1/platform/tenants/${tenantId}` },
        }),
      ),
    );

    const answer = await POST(
      new Request(`${web}/api/v1/platform/tenants`, {
        method: "POST",
        headers: { "Idempotency-Key": "key-1" },
        body: "{}",
      }),
    );

    expect(answer.headers.get("Location")).toBe(
      `/api/v1/platform/tenants/${tenantId}`,
    );
  });

  it.each([
    "/api/health",
    "/api/v2/x",
    "/api/v1/../me",
    "/api/v1/me%2F..%2Fplatform",
    "/api/v1/me%5C..%5Cplatform",
    "/api/v1/%2e%2e/me",
  ])("AnyPathOutsideV1_%s_Answers404AndCallsNothing", async (path) => {
    const answer = await GET(new Request(`${web}${path}`));

    expect(answer.status).toBe(404);
    expect(requests).toEqual([]);
  });
});
