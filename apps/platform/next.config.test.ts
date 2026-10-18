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
});
