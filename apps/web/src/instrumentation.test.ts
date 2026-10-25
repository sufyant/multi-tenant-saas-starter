import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// The SDK is faked: the test checks whether the server starts it and what onRequestError gives it (F55).
const sdk = vi.hoisted(() => ({
  init: vi.fn(),
  captureException: vi.fn(),
  captureRequestError: vi.fn(),
}));
vi.mock("@sentry/nextjs", () => sdk);

const variables = {
  NEXT_RUNTIME: "nodejs",
  API_URL: "https://api.example.test",
  CLERK_PUBLISHABLE_KEY: "pk_test_x",
  CLERK_SECRET_KEY: "sk_test_x",
};

beforeEach(() => {
  vi.resetModules();
  vi.clearAllMocks();
  for (const [name, value] of Object.entries(variables))
    vi.stubEnv(name, value);
});

afterEach(() => {
  vi.unstubAllEnvs();
});

describe("instrumentation", () => {
  it("Register_WithoutDsn_StartsNoTracking", async () => {
    vi.stubEnv("ERROR_TRACKING_DSN", "");
    const { register } = await import("./instrumentation");

    await register();

    expect(sdk.init).not.toHaveBeenCalled();
  });

  it("Register_WithDsn_StartsTheServerWithItsEnvironment", async () => {
    vi.stubEnv("ERROR_TRACKING_DSN", "https://public@errors.example.test/1");
    vi.stubEnv("ERROR_TRACKING_ENVIRONMENT", "production");
    const { register } = await import("./instrumentation");

    await register();

    expect(sdk.init).toHaveBeenCalledWith(
      expect.objectContaining({
        dsn: "https://public@errors.example.test/1",
        environment: "production",
      }),
    );
  });

  it("OnRequestError_ServerError_GoesToTheTracker", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const { onRequestError } = await import("./instrumentation");
    const error = new Error("render failed");
    const request = { path: "/", method: "GET", headers: {} };
    const context = {
      routerKind: "App Router",
      routePath: "/",
      routeType: "render",
      revalidateReason: undefined,
    } as const;

    await onRequestError(error, request, context);

    expect(sdk.captureRequestError).toHaveBeenCalledWith(
      error,
      request,
      context,
    );
  });
});
