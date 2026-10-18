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

test("DevComponents_OutsideProduction_ShowsEveryComponent", async ({
  page,
}) => {
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
