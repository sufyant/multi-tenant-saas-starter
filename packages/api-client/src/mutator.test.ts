import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  describe,
  expect,
  expectTypeOf,
  it,
  vi,
} from "vitest";
import {
  acceptInvitation,
  configureApiClient,
  createTenant,
  getMe,
  listMembers,
  listTenants,
  ProblemDetailsError,
  removeMember,
  suspendTenant,
  type TenantResponse,
  TenantResponseStatus,
} from "./index";
import { getListTenantsMockHandler, getListTenantsResponseMock } from "./msw";

// The browser calls the BFF at /api. Node's fetch needs an absolute address, so the tests use an origin of their own.
const baseUrl = "https://web.example.test/api";
const tenantId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";

const server = setupServer();
const requests: Request[] = [];

beforeAll(() => {
  server.listen({ onUnhandledFrame: "error" });
  server.events.on("request:start", ({ request }) => {
    requests.push(request.clone());
  });
});
afterEach(() => {
  server.resetHandlers();
  requests.length = 0;
});
afterAll(() => server.close());

describe("apiFetch", () => {
  it("ApiFetch_BeforeConfigureApiClient_ThrowsAndSendsNothing", async () => {
    vi.resetModules();
    const { apiFetch } = await import("./mutator");

    await expect(apiFetch("/v1/me")).rejects.toThrow(
      "configureApiClient() must run before the first API call.",
    );
    expect(requests).toEqual([]);
  });

  it("ListTenants_PlatformGet_SendsTheForwardedHeaders", async () => {
    configureApiClient({ baseUrl, headers: { "X-Request-Source": "test" } });
    const page = getListTenantsResponseMock();
    server.use(getListTenantsMockHandler(page));

    const answer = await listTenants({ page: 1, pageSize: 20 });

    expect(answer).toEqual(page);
    expect(requests[0]?.url).toBe(
      `${baseUrl}/v1/platform/tenants?page=1&pageSize=20`,
    );
    expect(requests[0]?.headers.get("X-Request-Source")).toBe("test");
    expect(requests[0]?.headers.has("Authorization")).toBe(false);
  });

  it("AcceptInvitation_MePost_SendsTheBody", async () => {
    configureApiClient({ baseUrl });
    server.use(
      http.post("*/v1/me/invitations/accept", () =>
        HttpResponse.json({
          id: tenantId,
          name: "Northwind MRO",
          slug: "northwind",
          status: "active",
        }),
      ),
    );

    const tenant = await acceptInvitation({ code: "invitation-code" });

    expect(tenant.slug).toBe("northwind");
    expect(requests[0]?.method).toBe("POST");
    expect(await requests[0]?.json()).toEqual({ code: "invitation-code" });
  });

  it("ListMembers_TenantOperation_PutsTheTenantInThePath", async () => {
    configureApiClient({ baseUrl });
    server.use(
      http.get("*/v1/tenants/:tenantId/members", () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 20, total: 0 }),
      ),
    );

    await listMembers(tenantId);

    expect(requests[0]?.url).toBe(`${baseUrl}/v1/tenants/${tenantId}/members`);
  });

  it("SuspendTenant_UnknownTenant_ThrowsTheProblemWithItsCode", async () => {
    configureApiClient({ baseUrl });
    server.use(
      http.post("*/v1/platform/tenants/:tenantId/suspend", () =>
        HttpResponse.json(
          { status: 404, code: "tenant.not_found", traceId: "00-trace" },
          {
            status: 404,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    const error = await suspendTenant(tenantId, {
      reason: "Unpaid invoices",
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ProblemDetailsError);
    expect((error as ProblemDetailsError).status).toBe(404);
    expect((error as ProblemDetailsError).problem?.code).toBe(
      "tenant.not_found",
    );
  });

  it("CreateTenant_WithIdempotencyKey_PassesTheKeyThrough", async () => {
    configureApiClient({ baseUrl });
    server.use(
      http.post("*/v1/platform/tenants", () =>
        HttpResponse.json(
          {
            id: tenantId,
            name: "Atlas Technic",
            slug: "atlas",
            status: "provisioning",
            suspensionReason: null,
            closedAt: null,
          },
          { status: 201 },
        ),
      ),
    );

    const tenant = await createTenant(
      {
        name: "Atlas Technic",
        slug: "atlas",
        ownerEmail: "owner@atlas.example",
      },
      { "Idempotency-Key": "key-1" },
    );

    expect(tenant.status).toBe(TenantResponseStatus.Provisioning);
    expect(requests[0]?.headers.get("Idempotency-Key")).toBe("key-1");
  });

  it("GetMe_ErrorAnswerThatIsNoProblemDetails_ThrowsWithoutAProblem", async () => {
    configureApiClient({ baseUrl });
    server.use(
      http.get(
        "*/v1/me",
        () => new HttpResponse("<html>Bad gateway</html>", { status: 502 }),
      ),
    );

    const error = await getMe().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ProblemDetailsError);
    expect((error as ProblemDetailsError).status).toBe(502);
    expect((error as ProblemDetailsError).problem).toBeNull();
  });

  it("RemoveMember_NoContent_ReturnsUndefined", async () => {
    configureApiClient({ baseUrl });
    server.use(
      http.delete(
        "*/v1/tenants/:tenantId/members/:userId",
        () => new HttpResponse(null, { status: 204 }),
      ),
    );

    expect(await removeMember(tenantId, tenantId)).toBeUndefined();
  });

  it("TenantResponseStatus_ValueTheDocumentDoesNotList_IsATypeError", () => {
    // @ts-expect-error A value that TenantResponseStatus does not list is not a tenant status.
    const status: TenantResponse["status"] = "archived";

    expectTypeOf(status).toEqualTypeOf<TenantResponse["status"]>();
  });
});
