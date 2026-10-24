import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { devComponentGroups } from "@repo/ui/dev-components-page";
import { expectAccessible } from "./axe";

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
  // The page's own list of groups, so a new component needs no change here.
  expect(sections.sort()).toEqual(
    devComponentGroups.map((name) => `dev-${name}`).sort(),
  );
  await expectAccessible(page);
});

test("DevComponents_ProductionBuild_IsNotFound", async ({ page }) => {
  test.skip(!productionBuild, "Only a production build leaves it out.");
  await setupClerkTestingToken({ page });

  const response = await page.goto("/dev/components");

  expect(response?.status()).toBe(404);
  await expectAccessible(page);
});
