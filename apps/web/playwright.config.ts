import { defineConfig, devices } from "@playwright/test";

// Playwright with Clerk's test mode (F35). The setup project signs in once through our sign-in page and keeps the session; the
// page tests reuse it. Each page test also runs axe (F37).
const port = Number(process.env.E2E_PORT ?? 3000);

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
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
  use: {
    baseURL: `http://localhost:${port}`,
    trace: "retain-on-failure",
  },
  projects: [
    { name: "setup", testMatch: /global\.setup\.ts/ },
    {
      name: "signed-out",
      testMatch: /sign-in\.spec\.ts/,
      use: { ...devices["Desktop Chrome"] },
    },
    {
      name: "signed-in",
      testIgnore: /(global\.setup|sign-in\.spec)\.ts/,
      dependencies: ["setup"],
      use: {
        ...devices["Desktop Chrome"],
        storageState: "playwright/.clerk/user.json",
      },
    },
  ],
});
