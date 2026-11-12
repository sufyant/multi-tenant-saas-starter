import { renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useFreshRouter } from "./use-fresh-router";

/** clerk-js, faked: it is not our code (F35). Its status starts as the test sets it, and `load` moves it on. */
const clerk = vi.hoisted(() => {
  const handlers = new Set<(status: string) => void>();
  const steps: string[] = [];
  return {
    status: "loading",
    steps,
    handlers,
    load(status: string) {
      this.status = status;
      for (const handler of [...handlers]) handler(status);
    },
    on(_event: string, handler: (status: string) => void) {
      handlers.add(handler);
      handler(this.status);
    },
    off(_event: string, handler: (status: string) => void) {
      handlers.delete(handler);
    },
    session: {
      getToken: vi.fn(async () => {
        steps.push("token");
        return "token";
      }),
    },
  };
});
vi.mock("@clerk/nextjs", () => ({ useClerk: () => clerk }));

const router = {
  push: vi.fn((href: string) => clerk.steps.push(`push ${href}`)),
  replace: vi.fn((href: string) => clerk.steps.push(`replace ${href}`)),
};
const fresh = () => renderHook(() => useFreshRouter(router)).result.current;

describe("useFreshRouter", () => {
  beforeEach(() => {
    clerk.status = "ready";
    clerk.steps.length = 0;
    clerk.handlers.clear();
    vi.clearAllMocks();
  });

  it("Push_ClerkLoaded_AsksForTheTokenFirst", async () => {
    await fresh().push("/northwind/settings/roles");

    expect(clerk.steps).toEqual(["token", "push /northwind/settings/roles"]);
  });

  it("Replace_ClerkLoaded_AsksForTheTokenFirst", async () => {
    await fresh().replace("/");

    expect(clerk.steps).toEqual(["token", "replace /"]);
  });

  it("Push_ClerkStillLoading_WaitsForIt", async () => {
    clerk.status = "loading";

    const pushed = fresh().push("/plans/1");
    await Promise.resolve();
    expect(router.push).not.toHaveBeenCalled();
    clerk.load("ready");
    await pushed;

    expect(clerk.steps).toEqual(["token", "push /plans/1"]);
    expect(clerk.handlers.size).toBe(0);
  });

  it("Push_ClerkFailedToLoad_StillNavigates", async () => {
    clerk.status = "error";

    await fresh().push("/plans/1");

    expect(router.push).toHaveBeenCalledExactlyOnceWith("/plans/1");
  });

  it("Push_TokenCannotBeRead_StillNavigates", async () => {
    clerk.session.getToken.mockRejectedValueOnce(new Error("offline"));

    await fresh().push("/plans/1");

    expect(router.push).toHaveBeenCalledExactlyOnceWith("/plans/1");
  });
});
