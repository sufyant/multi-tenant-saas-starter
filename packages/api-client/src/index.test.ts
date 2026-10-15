import { afterEach, describe, expect, it, vi } from "vitest";
import { type components, createApiClient } from "./index";

// Each call below compiles against the generated types. A generation that drops an operation, a path parameter or a status value
// fails `pnpm check-types` before any test runs.

const baseUrl = "https://api.example.test";
const tenantId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";

function answer(body: unknown, status = 200) {
  const fetch = vi.fn(
    async (_request: Request) =>
      new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
      }),
  );
  vi.stubGlobal("fetch", fetch);
  return fetch;
}

function sentRequest(fetch: ReturnType<typeof answer>): Request {
  const request = fetch.mock.calls[0]?.[0];
  if (!request) throw new Error("No request was sent.");
  return request;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("createApiClient", () => {
  it("calls an admin operation with the bearer token", async () => {
    const tenant: components["schemas"]["TenantResponse"] = {
      id: tenantId,
      name: "Acme",
      slug: "acme",
      status: "active",
    };
    const fetch = answer({ items: [tenant], page: 1, pageSize: 20, total: 1 });
    const client = createApiClient({
      baseUrl,
      getToken: async () => "token-1",
    });

    const { data } = await client.GET("/v1/admin/tenants", {
      params: { query: { page: 1, pageSize: 20 } },
    });

    expect(data?.items?.[0]?.status).toBe("active");
    const request = sentRequest(fetch);
    expect(request.url).toBe(`${baseUrl}/v1/admin/tenants?page=1&pageSize=20`);
    expect(request.headers.get("Authorization")).toBe("Bearer token-1");
  });

  it("calls a me operation with its body", async () => {
    const fetch = answer({
      id: tenantId,
      name: "Acme",
      slug: "acme",
      status: "active",
    });
    const client = createApiClient({ baseUrl, getToken: () => "token-2" });

    const { data } = await client.POST("/v1/me/invitations/accept", {
      body: { code: "invitation-code" },
    });

    expect(data?.slug).toBe("acme");
    const request = sentRequest(fetch);
    expect(request.method).toBe("POST");
    expect(await request.json()).toEqual({ code: "invitation-code" });
  });

  it("calls a tenant operation with the tenant in the path", async () => {
    const fetch = answer({
      items: [{ userId: tenantId, role: "Owner" }],
      page: 1,
      pageSize: 20,
      total: 1,
    });
    const client = createApiClient({
      baseUrl,
      getToken: async () => "token-3",
    });

    const { data } = await client.GET("/v1/tenants/{tenantId}/members", {
      params: { path: { tenantId } },
    });

    expect(data?.items?.[0]?.role).toBe("Owner");
    expect(sentRequest(fetch).url).toBe(
      `${baseUrl}/v1/tenants/${tenantId}/members`,
    );
  });

  it("reads the error code of a refused request", async () => {
    answer({ status: 404, code: "tenant.not_found" }, 404);
    const client = createApiClient({
      baseUrl,
      getToken: async () => "token-4",
    });

    const { error } = await client.POST(
      "/v1/admin/tenants/{tenantId}/suspend",
      {
        params: { path: { tenantId } },
      },
    );

    expect(error?.code).toBe("tenant.not_found");
  });

  it("sends no Authorization header without a token", async () => {
    const fetch = answer({ items: [], page: 1, pageSize: 20, total: 0 });
    const client = createApiClient({ baseUrl, getToken: async () => null });

    await client.GET("/v1/me/tenants");

    expect(sentRequest(fetch).headers.has("Authorization")).toBe(false);
  });

  it("knows the status values of a response", () => {
    // @ts-expect-error A status that ResponseStatus does not list is not a status.
    const status: components["schemas"]["TenantResponse"]["status"] =
      "archived";

    expect(status).toBe("archived");
  });
});
