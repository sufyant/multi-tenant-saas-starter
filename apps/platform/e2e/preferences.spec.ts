import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { Language, type RenameMeRequest } from "@repo/api-client";
import { mockApi, mockRename } from "./api";
import { expectAccessible } from "./axe";

test("Preferences_English_StartsWithThePlatformLabel", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.En);

  await page.goto("/settings/preferences");

  await expect(
    page.getByRole("heading", { level: 1, name: "Preferences" }),
  ).toBeVisible();
  await expect(page.getByRole("banner")).toContainText("Platform");
  await expectAccessible(page);
});

test("Preferences_Arabic_IsRightToLeft", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.Ar);

  await page.goto("/settings/preferences");

  await expect(
    page.getByRole("heading", { level: 1, name: "التفضيلات" }),
  ).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await expectAccessible(page);
});

test("Preferences_Rename_ShowsTheNewNameInTheUserMenu", async ({ page }) => {
  const bodies: RenameMeRequest[] = [];
  await setupClerkTestingToken({ page });
  await mockRename(page, Language.En, bodies);

  await page.goto("/settings/preferences");
  await expect(page.getByRole("button", { name: "SK Account" })).toBeVisible();
  await expect(page.getByText("admin@starter.example")).toBeVisible();
  await page.getByRole("textbox", { name: "First name" }).fill("Sasha");
  await page.getByRole("button", { name: "Save preferences" }).click();

  await expect(page.getByText("Preferences saved")).toBeVisible();
  await expectAccessible(page);
  expect(bodies).toEqual([{ firstName: "Sasha", lastName: "Kelly" }]);
  await page.getByRole("button", { name: "SK Account" }).click();
  await expect(page.getByText("Sasha Kelly")).toBeVisible();
});
