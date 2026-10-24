import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { mockPlatform, organisations } from "./api";
import { expectAccessible } from "./axe";

// The platform's Activity, and the platform's entries about one organisation on its Activity tab (the Activity boards).

const open = async (page: Page, path: string) => {
  await setupClerkTestingToken({ page });
  await mockPlatform(page);
  await page.goto(path);
};

test("Activity_Platform_ShowsEveryEntryNewestFirst", async ({ page }) => {
  await open(page, "/activity");

  await expect(
    page.getByRole("heading", { level: 1, name: "Activity" }),
  ).toBeVisible();
  const entries = page.getByRole("main").getByRole("listitem");
  await expect(entries).toHaveText([
    /Sam Kelly created Atlas Logistics and invited its Owner\./,
    /Sam Kelly suspended Shannon Studio\. Reason: Unpaid invoice\./,
    /Sam Kelly made Sam Kelly a system admin\./,
    /Sam Kelly closed Kestrel Labs\./,
  ]);
  await expectAccessible(page);
});

test("Activity_OrganisationTab_ShowsOnlyItsEntries", async ({ page }) => {
  await open(page, `/organisations/${organisations.kestrel.id}`);

  await page.getByRole("tab", { name: "Activity" }).click();

  await expect(page).toHaveURL(/tab=activity/);
  const entries = page.getByRole("main").getByRole("listitem");
  await expect(entries).toHaveText([/Sam Kelly closed Kestrel Labs\./]);
  await expectAccessible(page);
});
