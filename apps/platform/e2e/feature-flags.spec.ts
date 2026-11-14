import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { mockPlatform } from "./api";
import { expectAccessible } from "./axe";
import { mockFrameFlags } from "./flag-frames";

// Settings › Feature flags (step 39): the list, a flag's page and a confirmation, each checked by axe (F37). The API is mocked
// with the flags of the frames (`flag-frames.ts`).

const open = async (page: Page, path: string) => {
  await setupClerkTestingToken({ page });
  await mockPlatform(page);
  await mockFrameFlags({ previewCount: 8 })(page);
  await page.goto(path);
};

test.afterEach(({ page }) => page.unrouteAll({ behavior: "ignoreErrors" }));

test("FeatureFlags_Opens_ShowsTheFlagsAndPassesAxe", async ({ page }) => {
  await open(page, "/settings/feature-flags");

  await expect(
    page
      .getByRole("table", { name: "Feature flags" })
      .getByText("10% · 1 organisation"),
  ).toBeVisible();
  await expectAccessible(page);
});

test("FeatureFlag_Opens_ShowsItsRulesAndPassesAxe", async ({ page }) => {
  await open(page, "/settings/feature-flags/roles.bulk_edit");

  await expect(
    page.getByRole("region", { name: "Who it is on for" }),
  ).toBeVisible();
  await expectAccessible(page);
});

test("FeatureFlag_TurnOnForEveryone_AsksWithTheCountAndPassesAxe", async ({
  page,
}) => {
  await open(page, "/settings/feature-flags/roles.bulk_edit");

  await page.getByRole("button", { name: "Turn on for everyone" }).click();

  await expect(
    page.getByRole("alertdialog").getByText(/8 more organisations get it now/),
  ).toBeVisible();
  await expectAccessible(page);
});
