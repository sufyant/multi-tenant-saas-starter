import { describe, expect, it } from "vitest";
import { buildTokensCss } from "./build-tokens.mjs";

const tokens = (colors: { name: string; value: Record<string, string> }[]) => ({
  color: { tokens: colors },
  type: { families: { sans: '"Geist", sans-serif' }, groups: [] },
  spacing: { tokens: [{ name: "space-1", value: "4px" }] },
  radius: { tokens: [] },
  shadow: { tokens: [] },
  size: { tokens: [] },
  breakpoint: { tokens: [] },
  zIndex: { tokens: [] },
});

describe("buildTokensCss", () => {
  it("BuildTokensCss_ColorThatNamesAnotherToken_ResolvesToItsVariable", () => {
    const css = buildTokensCss(
      tokens([
        { name: "primary", value: { light: "#2747d8", dark: "#7088ff" } },
        { name: "ring", value: { light: "{primary}", dark: "{primary}" } },
      ]),
    );

    expect(css).toContain("--ring: var(--primary);");
    expect(css).toContain("--primary: #2747d8;");
    expect(css).toContain("--primary: #7088ff;");
  });

  it("BuildTokensCss_ColorWithoutADarkValue_Fails", () => {
    const build = () => buildTokensCss(tokens([{ name: "primary", value: { light: "#2747d8" } }]));

    expect(build).toThrow("primary has no dark value");
  });

  it("BuildTokensCss_ReferenceToAnUnknownToken_Fails", () => {
    const build = () => buildTokensCss(tokens([{ name: "ring", value: { light: "{nothing}", dark: "{nothing}" } }]));

    expect(build).toThrow("ring names an unknown token {nothing}");
  });

  it("BuildTokensCss_RunTwice_WritesTheSameText", () => {
    const source = tokens([{ name: "primary", value: { light: "#2747d8", dark: "#7088ff" } }]);

    expect(buildTokensCss(source)).toBe(buildTokensCss(source));
  });

  it("BuildTokensCss_RealTokens_WritesEveryKindOfToken", async () => {
    const { readFile } = await import("node:fs/promises");
    const source = JSON.parse(await readFile(new URL("../tokens/tokens.json", import.meta.url), "utf8"));

    const css = buildTokensCss(source);

    expect(css).toContain("--background: #fbfbfa;");
    expect(css).toContain("--space-4: 16px;");
    expect(css).toContain("--radius-md: 6px;");
    expect(css).toContain("--control-md: 32px;");
    expect(css).toContain("--z-toast: 500;");
    expect(css).toContain("--bp-tablet: 768px;");
    expect(css).toContain("--font-page-title-size: 22px;");
    expect(css).toContain("--font-page-title-tracking: -0.02em;");
    expect(css).toContain("--font-mono:");
  });
});
