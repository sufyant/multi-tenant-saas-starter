import { describe, expect, it } from "vitest";
import { buildTokensCss } from "./build-tokens.mjs";

// The @theme inline block that globals.css held by hand before build-tokens.mjs wrote it (F20). The script must write it unchanged.
const handWrittenThemeBlock = `@theme inline {
  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-card: var(--card);
  --color-card-foreground: var(--card-foreground);
  --color-popover: var(--popover);
  --color-popover-foreground: var(--popover-foreground);
  --color-muted: var(--muted);
  --color-muted-foreground: var(--muted-foreground);
  --color-accent: var(--accent);
  --color-accent-foreground: var(--accent-foreground);
  --color-secondary: var(--secondary);
  --color-secondary-foreground: var(--secondary-foreground);
  --color-primary: var(--primary);
  --color-primary-foreground: var(--primary-foreground);
  --color-primary-hover: var(--primary-hover);
  --color-primary-subtle: var(--primary-subtle);
  --color-primary-subtle-foreground: var(--primary-subtle-foreground);
  --color-border: var(--border);
  --color-input: var(--input);
  --color-ring: var(--ring);
  --color-destructive: var(--destructive);
  --color-destructive-foreground: var(--destructive-foreground);
  --color-destructive-text: var(--destructive-text);
  --color-destructive-subtle: var(--destructive-subtle);
  --color-success: var(--success);
  --color-success-text: var(--success-text);
  --color-success-subtle: var(--success-subtle);
  --color-warning: var(--warning);
  --color-warning-text: var(--warning-text);
  --color-warning-subtle: var(--warning-subtle);
  --color-info: var(--info);
  --color-info-text: var(--info-text);
  --color-info-subtle: var(--info-subtle);
  --color-neutral: var(--neutral);
  --color-neutral-text: var(--neutral-text);
  --color-sidebar: var(--sidebar);
  --color-sidebar-foreground: var(--sidebar-foreground);
  --color-sidebar-accent: var(--sidebar-accent);
  --color-sidebar-accent-foreground: var(--sidebar-accent-foreground);
  --color-sidebar-border: var(--sidebar-border);
  --color-platform: var(--platform);
  --color-platform-foreground: var(--platform-foreground);
  --color-overlay: var(--overlay);

  --spacing-control-md: var(--control-md);
  --spacing-control-lg: var(--control-lg);
  --spacing-control-xl: var(--control-xl);
  --spacing-touch-target: var(--touch-target);
  --spacing-touch-target-tablet: var(--touch-target-tablet);
  --spacing-rail-width: var(--rail-width);
  --spacing-rail-expanded: var(--rail-expanded);
  --container-content-max: var(--content-max);

  --shadow-xs: var(--shadow-xs);
  --shadow-md: var(--shadow-md);
  --shadow-lg: var(--shadow-lg);

  --font-sans: var(--font-sans);
  --font-mono: var(--font-mono);
  --font-arabic: var(--font-arabic);

  --text-page-title: var(--font-page-title-size);
  --text-page-title--line-height: var(--font-page-title-line-height);
  --text-page-title--font-weight: var(--font-page-title-weight);
  --text-page-title--letter-spacing: var(--font-page-title-tracking);
  --text-section-title: var(--font-section-title-size);
  --text-section-title--line-height: var(--font-section-title-line-height);
  --text-section-title--font-weight: var(--font-section-title-weight);
  --text-section-title--letter-spacing: var(--font-section-title-tracking);
  --text-body: var(--font-body-size);
  --text-body--line-height: var(--font-body-line-height);
  --text-small: var(--font-small-size);
  --text-small--line-height: var(--font-small-line-height);
  --text-status: var(--font-status-size);
  --text-status--line-height: var(--font-status-line-height);
  --text-status--font-weight: var(--font-status-weight);
  --text-mono: var(--font-mono-size);
  --text-mono--line-height: var(--font-mono-line-height);

  --z-index-sticky: var(--z-sticky);
  --z-index-drawer: var(--z-drawer);
  --z-index-dialog: var(--z-dialog);
  --z-index-popover: var(--z-popover);
  --z-index-toast: var(--z-toast);
}`;

/** The `@theme inline` block of the CSS, from its first line to its closing brace. */
const themeBlock = (css: string) => {
  const start = css.indexOf("@theme inline {");
  return css.slice(start, css.indexOf("\n}", start) + 2);
};

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
    const build = () =>
      buildTokensCss(
        tokens([{ name: "primary", value: { light: "#2747d8" } }]),
      );

    expect(build).toThrow("primary has no dark value");
  });

  it("BuildTokensCss_ReferenceToAnUnknownToken_Fails", () => {
    const build = () =>
      buildTokensCss(
        tokens([
          { name: "ring", value: { light: "{nothing}", dark: "{nothing}" } },
        ]),
      );

    expect(build).toThrow("ring names an unknown token {nothing}");
  });

  it("BuildTokensCss_RunTwice_WritesTheSameText", () => {
    const source = tokens([
      { name: "primary", value: { light: "#2747d8", dark: "#7088ff" } },
    ]);

    expect(buildTokensCss(source)).toBe(buildTokensCss(source));
  });

  it("BuildTokensCss_RealTokens_WritesEveryKindOfToken", async () => {
    const { readFile } = await import("node:fs/promises");
    const source = JSON.parse(
      await readFile(`${import.meta.dirname}/../tokens/tokens.json`, "utf8"),
    );

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

  it("BuildTokensCss_Breakpoint_GoesIntoTheTailwindThemeAsAValue", () => {
    const source = {
      ...tokens([]),
      breakpoint: { tokens: [{ name: "bp-tablet", value: "768px" }] },
    };

    expect(buildTokensCss(source)).toContain(
      "@theme {\n  --breakpoint-tablet: 768px;\n}",
    );
  });

  it("BuildTokensCss_Shadow_HasALightAndADarkValue", () => {
    const source = {
      ...tokens([]),
      shadow: {
        tokens: [
          {
            name: "shadow-md",
            value: { light: "0 4px 16px black", dark: "0 8px 24px black" },
          },
        ],
      },
    };

    const css = buildTokensCss(source);

    expect(css.slice(0, css.indexOf(".dark"))).toContain(
      "--shadow-md: 0 4px 16px black;",
    );
    expect(css.slice(css.indexOf(".dark"))).toContain(
      "--shadow-md: 0 8px 24px black;",
    );
  });

  it("BuildTokensCss_RealTokens_WritesTheThemeBlockThatGlobalsCssHeld", async () => {
    const { readFile } = await import("node:fs/promises");
    const source = JSON.parse(
      await readFile(`${import.meta.dirname}/../tokens/tokens.json`, "utf8"),
    );

    const css = buildTokensCss(source);

    expect(themeBlock(css).split("\n")).toEqual(
      handWrittenThemeBlock.split("\n"),
    );
  });

  it("BuildTokensCss_NewColorToken_GoesIntoTheThemeAsAColor", () => {
    const css = buildTokensCss(
      tokens([{ name: "brand", value: { light: "#123456", dark: "#654321" } }]),
    );

    expect(themeBlock(css)).toContain("  --color-brand: var(--brand);");
  });
});
