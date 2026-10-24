import { defineConfig, devices } from "@playwright/test";

// Playwright with Clerk's test mode (F35). The setup project makes a temporary user, enrols its authenticator app through our
// screens and keeps a session with the second factor; the page tests reuse it, and the teardown deletes the user (decision 5 of
// step 21b). Each page test also runs axe (F37).
const port = Number(process.env.E2E_PORT ?? 3001);

export default defineConfig({
  testDir: "./e2e",
  globalSetup: "./e2e/clerk.global-setup.ts",
  fullyParallel: true,
  retries: 0,
  webServer: {
    command: process.env.CI
      ? `pnpm start --port ${port}`
      : `pnpm dev --port ${port}`,
    url: `http://localhost:${port}/sign-in`,
    // The dev server leaves out Next.js's indicator, which covers the rail's toggle (next.config.ts). A server that is already
    // running keeps its own settings.
    env: { E2E_DEV_SERVER: "1" },
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
  use: {
    baseURL: `http://localhost:${port}`,
    trace: "retain-on-failure",
  },
  projects: [
    {
      name: "setup",
      testMatch: /global\.setup\.ts/,
      teardown: "teardown",
      // The setup types the authenticator's secret and the backup codes: no trace may record them.
      use: { trace: "off" },
    },
    { name: "teardown", testMatch: /global\.teardown\.ts/ },
    {
      name: "signed-out",
      testMatch: /sign-in\.spec\.ts/,
      use: { ...devices["Desktop Chrome"] },
    },
    {
      name: "signed-in",
      testIgnore:
        /(global\.setup|global\.teardown|sign-in\.spec|two-step\.spec)\.ts/,
      dependencies: ["setup"],
      use: {
        ...devices["Desktop Chrome"],
        storageState: "playwright/.clerk/user.json",
      },
    },
    {
      name: "two-step",
      testMatch: /two-step\.spec\.ts/,
      dependencies: ["setup"],
      use: { ...devices["Desktop Chrome"], trace: "off" },
    },
  ],
});
