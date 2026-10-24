import { afterEach, describe, expect, it, vi } from "vitest";

afterEach(() => {
  vi.unstubAllEnvs();
  vi.resetModules();
});

describe("next.config", () => {
  it("PageExtensions_ProductionBuild_LeaveOutTheDevPages", async () => {
    vi.stubEnv("NODE_ENV", "production");

    const { default: config } = await import("./next.config");

    expect(config.pageExtensions).not.toContain("dev.tsx");
  });

  it("PageExtensions_Development_HoldTheDevPages", async () => {
    vi.stubEnv("NODE_ENV", "development");

    const { default: config } = await import("./next.config");

    expect(config.pageExtensions).toContain("dev.tsx");
  });

  it("DevIndicators_PlaywrightDevServer_AreOff", async () => {
    vi.stubEnv("E2E_DEV_SERVER", "1");

    const { default: config } = await import("./next.config");

    expect(config.devIndicators).toBe(false);
  });

  it("DevIndicators_Development_AreNextJsDefault", async () => {
    vi.stubEnv("E2E_DEV_SERVER", "");

    const { default: config } = await import("./next.config");

    expect(config.devIndicators).toBeUndefined();
  });
});
