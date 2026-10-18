import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { expectAccessible } from "./axe";

const names = [
  "AppShell",
  "Banner",
  "Button",
  "Combobox",
  "Dialog",
  "EmptyState",
  "Freshness",
  "Icon",
  "Input",
  "OrgMenu",
  "RowMenu",
  "Skeleton",
  "Status",
  "SubNav",
  "Table",
  "Tabs",
  "Tag",
  "Time",
  "Toast",
  "UserMenu",
];

// CI runs the production build (`pnpm start`, playwright.config.ts), and a production build has no /dev/components (F33). So
// each test runs on one kind of build only.
const productionBuild = Boolean(process.env.CI);

test("DevComponents_OutsideProduction_ShowsEveryComponent", async ({
  page,
}) => {
  test.skip(productionBuild, "A production build has no /dev/components.");
  await setupClerkTestingToken({ page });

  await page.goto("/dev/components");

  await expect(page.getByRole("region", { name: "AppShell" })).toBeVisible();
  const sections = await page
    .locator("section[aria-labelledby^='dev-']")
    .evaluateAll((regions) =>
      regions.map((region) => region.getAttribute("aria-labelledby")),
    );
  expect(sections.sort()).toEqual(names.map((name) => `dev-${name}`));
  await expectAccessible(page);
});

test("DevComponents_ProductionBuild_IsNotFound", async ({ page }) => {
  test.skip(!productionBuild, "Only a production build leaves it out.");
  await setupClerkTestingToken({ page });

  const response = await page.goto("/dev/components");

  expect(response?.status()).toBe(404);
  await expectAccessible(page);
});
