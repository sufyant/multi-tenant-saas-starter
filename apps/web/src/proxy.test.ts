import { NextRequest } from "next/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

// The proxy's own handler, with Clerk's route matcher and a stand-in for auth(), so a test sees whether a path needs a session.
type Handler = (
  auth: { protect: () => Promise<unknown> },
  request: NextRequest,
) => Promise<unknown>;
const handler = vi.hoisted(() => ({
  current: undefined as Handler | undefined,
}));
vi.mock("@clerk/nextjs/server", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@clerk/nextjs/server")>()),
  clerkMiddleware: (own: Handler) => {
    handler.current = own;
    return own;
  },
}));
await import("./proxy");

const protect = vi.fn();
beforeEach(() => protect.mockReset());

async function needsSession(path: string): Promise<boolean> {
  await handler.current?.(
    { protect },
    new NextRequest(`https://web.example.test${path}`),
  );
  return protect.mock.calls.length > 0;
}

describe("proxy", () => {
  it.each([
    "/sign-in",
    "/sign-in/factor-one",
    "/sso-callback",
    "/sso-callback/x",
    "/invitations/accept",
    "/invitations/accept/x",
    "/health",
    "/api/v1/me",
  ])("Proxy_OpenRoute_%s_NeedsNoSession", async (path) => {
    expect(await needsSession(path)).toBe(false);
  });

  // A tenant's slug is the first segment (F30), so a slug that only starts with an open route's name still needs a session.
  it.each([
    "/sign-inn",
    "/sign-inn/settings",
    "/sso-callbacks",
    "/sso-callbacks/settings",
    "/invitations/acceptx",
    "/healthy",
    "/apis",
    "/atlas",
  ])("Proxy_LongerSegment_%s_NeedsASession", async (path) => {
    expect(await needsSession(path)).toBe(true);
  });

  // No route serves /trpc: the first pattern of the matcher runs the proxy on every path but the static files.
  it("Config_Matcher_NamesNoTrpc", async () => {
    const { config } = await import("./proxy");

    expect(config.matcher.join(" ")).not.toContain("trpc");
  });
});
