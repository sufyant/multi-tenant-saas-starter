import { NextRequest } from "next/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

// The proxy's own handler, with Clerk's route matcher and a stand-in for auth(), so a test sees whether a path needs a session.
type Handler = (
  auth: {
    (): Promise<{ userId: string | null }>;
    protect: () => Promise<unknown>;
  },
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
// auth() of a person signed in as the user, or signed out with null, and its protect().
const authOf = (userId: string | null) =>
  Object.assign(async () => ({ userId }), { protect });
beforeEach(() => protect.mockReset());

async function needsSession(path: string): Promise<boolean> {
  await handler.current?.(
    authOf("user_1"),
    new NextRequest(`https://web.example.test${path}`),
  );
  return protect.mock.calls.length > 0;
}

// A request of the browser, as the proxy sees it: a navigation of the App Router is a fetch (Sec-Fetch-Dest: empty), a page
// load a document.
async function navigate(
  userId: string | null,
  destination = "empty",
): Promise<unknown> {
  return handler.current?.(
    authOf(userId),
    new NextRequest("https://web.example.test/northwind/settings/general", {
      headers: { "Sec-Fetch-Dest": destination, "Next-Url": "/" },
    }),
  );
}

describe("proxy", () => {
  it.each([
    "/sign-in",
    "/sign-in/factor-one",
    "/sso-callback",
    "/sso-callback/x",
    "/invitations/accept",
    "/invitations/accept/x",
    "/account/deleted",
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
    "/account",
    "/account/deletedx",
    "/healthy",
    "/apis",
    "/atlas",
  ])("Proxy_LongerSegment_%s_NeedsASession", async (path) => {
    expect(await needsSession(path)).toBe(true);
  });

  // A signed-out navigation loads the page as a document (F63). Which request is a navigation: `navigation.test.ts` of packages/ui.
  it("Proxy_AClientSideNavigationSignedOut_AsksTheBrowserToLoadThePage", async () => {
    const answer = await navigate(null);

    expect(protect).not.toHaveBeenCalled();
    expect((answer as Response).status).toBe(401);
    expect((answer as Response).body).toBeNull();
  });

  it("Proxy_APageLoadSignedOut_NeedsTheSession", async () => {
    await navigate(null, "document");

    expect(protect).toHaveBeenCalled();
  });

  it("Proxy_AClientSideNavigationSignedIn_NeedsTheSession", async () => {
    await navigate("user_1");

    expect(protect).toHaveBeenCalled();
  });

  // No route serves /trpc: the first pattern of the matcher runs the proxy on every path but the static files.
  it("Config_Matcher_NamesNoTrpc", async () => {
    const { config } = await import("./proxy");

    expect(config.matcher.join(" ")).not.toContain("trpc");
  });
});
