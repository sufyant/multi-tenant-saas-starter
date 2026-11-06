import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { Language, type RenameMeRequest } from "@repo/api-client";
import { mockApi, mockRename } from "./api";
import { expectAccessible } from "./axe";

test("Profile_English_ShowsThePlatformsSettingsMenu", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.En);

  await page.goto("/settings/profile");

  await expect(
    page.getByRole("heading", { level: 1, name: "Profile" }),
  ).toBeVisible();
  await expect(
    page.getByRole("navigation", { name: "Settings menu" }),
  ).toContainText("Back to the platform");
  await expectAccessible(page);
});

test("Profile_Arabic_IsRightToLeft", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.Ar);

  await page.goto("/settings/profile");

  await expect(
    page.getByRole("heading", { level: 1, name: "الملف الشخصي" }),
  ).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await expectAccessible(page);
});

test("Profile_Rename_ShowsTheNewNameInTheUserMenu", async ({ page }) => {
  const bodies: RenameMeRequest[] = [];
  await setupClerkTestingToken({ page });
  await mockRename(page, Language.En, bodies);

  await page.goto("/settings/profile");
  await expect(page.getByRole("button", { name: /Kelly/ })).toBeVisible();
  await page.getByRole("textbox", { name: "Name" }).fill("Sasha Kelly");
  await page.getByRole("button", { name: "Save preferences" }).click();

  await expect(page.getByText("Preferences saved")).toBeVisible();
  await expectAccessible(page);
  expect(bodies).toEqual([{ name: "Sasha Kelly" }]);
  await page.getByRole("button", { name: /Sasha Kelly/ }).click();
  await expect(page.getByRole("menu").getByText("Sasha Kelly")).toBeVisible();
});
