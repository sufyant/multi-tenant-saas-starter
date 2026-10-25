import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { CodeStep, SecondFactorStep } from "@repo/ui/components/sign-in";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterAll, beforeAll, describe, expect, it, vi } from "vitest";
import { renderWithProviders } from "../../test/render";
import { browserTracking } from "./browser";
import { browserIntegrations, browserOptions } from "./options";

// Vitest loads the SDK's Node build. This test needs the browser's, with its DOM breadcrumbs, so it requires the client build by its
// path. A fake transport keeps every envelope here: nothing leaves the test.
type BrowserSdk = typeof import("@sentry/nextjs") & {
  getDefaultIntegrations: (options: object) => { name: string }[];
};
const require = createRequire(import.meta.url);
const sdkPath = join(
  dirname(require.resolve("@sentry/nextjs/package.json")),
  "build/cjs/index.client.js",
);
const envelopes: unknown[] = [];
let sdk: BrowserSdk;

beforeAll(() => {
  sdk = require(sdkPath) as BrowserSdk;
  sdk.init({
    ...browserOptions({
      dsn: "https://public@errors.example.test/1",
      environment: "staging",
    }),
    transport: () => ({
      send: async (envelope: unknown) => {
        envelopes.push(envelope);
        return {};
      },
      flush: async () => true,
    }),
  });
});

afterAll(async () => {
  await sdk.close();
});

// The event that the SDK would send, with its breadcrumbs, as text.
async function sentText(): Promise<string> {
  envelopes.length = 0;
  sdk.captureException(new Error("probe"));
  await sdk.flush(1000);
  return JSON.stringify(envelopes);
}

describe("the browser's breadcrumbs", () => {
  it("BrowserOptions_Integrations_KeepTheBreadcrumbsAndDropSessionsAndTracing", () => {
    const kept = sdk
      .getClient()
      ?.getOptions()
      .integrations.map((integration) => integration.name);

    expect(kept).toEqual(
      expect.arrayContaining(["Breadcrumbs", "GlobalHandlers"]),
    );
    expect(kept).not.toContain("BrowserSession");
    expect(kept).not.toContain("BrowserTracing");
    expect(kept?.every((name) => browserIntegrations.includes(name))).toBe(
      true,
    );
  });

  it("InputBreadcrumb_CodeStep_NamesTheElementNotTheDigits", async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <CodeStep
        email="ali@northwind.io"
        onSubmit={vi.fn()}
        onResend={vi.fn()}
        onUseAnotherEmail={vi.fn()}
      />,
    );

    await user.type(screen.getByRole("textbox"), "917253");
    const sent = await sentText();

    expect(sent).toContain('"category":"ui.input"');
    expect(sent).toContain("input");
    expect(sent).not.toContain("917253");
  });

  it("InputBreadcrumb_BackupCode_NamesTheElementNotTheCode", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SecondFactorStep onSubmit={vi.fn()} exit={null} />);

    await user.click(screen.getByRole("button", { name: "Use a backup code" }));
    await user.type(
      screen.getByRole("textbox", { name: "Backup code" }),
      "k7p2-9xq4",
    );
    const sent = await sentText();

    expect(sent).toContain('"category":"ui.input"');
    expect(sent).not.toContain("k7p2");
    expect(sent).not.toContain("9xq4");
  });
});

// The root layout writes the DSN and the environment on <html> only when the server has them (F55).
describe("browserTracking", () => {
  it("BrowserTracking_DsnAndEnvironment_StartsWithThem", () => {
    expect(
      browserTracking({
        errorTrackingDsn: "https://public@errors.example.test/1",
        errorTrackingEnvironment: "production",
      }),
    ).toEqual({
      dsn: "https://public@errors.example.test/1",
      environment: "production",
    });
  });

  it.each([
    ["no DSN", {}],
    [
      "a DSN without an environment",
      { errorTrackingDsn: "https://public@errors.example.test/1" },
    ],
    [
      "an unknown environment",
      {
        errorTrackingDsn: "https://public@errors.example.test/1",
        errorTrackingEnvironment: "local",
      },
    ],
  ])("BrowserTracking_%s_DoesNotStart", (_, dataset) => {
    expect(browserTracking(dataset)).toBeUndefined();
  });
});
