import { NextRequest } from "next/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

// The proxy's own handler, with Clerk's route matcher and a stand-in for auth(), so a test sees whether a path needs a session
// and whether the two-step gate (F42) sends it to /two-step.
type Handler = (
  auth: {
    (): Promise<{ userId: string | null }>;
    protect: () => Promise<unknown>;
  },
  request: NextRequest,
) => Promise<Response | undefined>;
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
// A session without a verified second factor: Clerk's -1.
beforeEach(() => {
  protect.mockReset();
  protect.mockResolvedValue({ factorVerificationAge: [0, -1] });
});

function run(path: string) {
  return handler.current?.(
    authOf("user_1"),
    new NextRequest(`https://platform.example.test${path}`),
  );
}

// A request of the browser, as the proxy sees it: a navigation of the App Router is a fetch (Sec-Fetch-Dest: empty), a page
// load a document.
function navigate(userId: string | null, destination = "empty") {
  return handler.current?.(
    authOf(userId),
    new NextRequest("https://platform.example.test/organisations", {
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
    "/health",
    "/api/v1/me",
  ])("Proxy_OpenRoute_%s_NeedsNoSession", async (path) => {
    await run(path);

    expect(protect).not.toHaveBeenCalled();
  });

  it.each([
    "/sign-inn",
    "/sign-inn/x",
    "/sso-callbacks",
    "/sso-callbacks/x",
    "/healthy",
    "/apis",
  ])("Proxy_LongerSegment_%s_NeedsASession", async (path) => {
    await run(path);

    expect(protect).toHaveBeenCalled();
  });

  it.each(["/two-step", "/two-step/x"])(
    "Proxy_TwoStep_%s_PassesWithoutASecondFactor",
    async (path) => {
      const response = await run(path);

      expect(protect).toHaveBeenCalled();
      expect(response).toBeUndefined();
    },
  );

  it.each(["/two-steps", "/two-steps/x", "/organisations"])(
    "Proxy_OtherRoute_%s_GoesToTheGateWithoutASecondFactor",
    async (path) => {
      const response = await run(path);

      expect(response?.headers.get("location")).toBe(
        "https://platform.example.test/two-step",
      );
    },
  );

  it("Proxy_SecondFactorVerified_Passes", async () => {
    protect.mockResolvedValue({ factorVerificationAge: [0, 0] });

    expect(await run("/two-steps")).toBeUndefined();
  });

  // No route serves /trpc: the first pattern of the matcher runs the proxy on every path but the static files.
  it("Config_Matcher_NamesNoTrpc", async () => {
    const { config } = await import("./proxy");

    expect(config.matcher.join(" ")).not.toContain("trpc");
  });

  // A signed-out navigation loads the page as a document (F63).
  it("Proxy_AClientSideNavigationSignedOut_AsksTheBrowserToLoadThePage", async () => {
    const answer = await navigate(null);

    expect(protect).not.toHaveBeenCalled();
    expect(answer?.status).toBe(401);
    expect(answer?.body).toBeNull();
  });

  it("Proxy_APageLoadSignedOut_NeedsTheSession", async () => {
    await navigate(null, "document");

    expect(protect).toHaveBeenCalled();
  });

  it("Proxy_AClientSideNavigationSignedIn_NeedsTheSession", async () => {
    await navigate("user_1");

    expect(protect).toHaveBeenCalled();
  });
});
