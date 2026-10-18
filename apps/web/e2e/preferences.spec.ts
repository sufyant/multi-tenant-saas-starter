import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { Language } from "@repo/api-client";
import { mockApi, northwind } from "./api";
import { expectAccessible } from "./axe";

test("Preferences_English_IsLeftToRight", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.en);

  await page.goto(`/${northwind.slug}/settings/preferences`);

  await expect(
    page.getByRole("heading", { level: 1, name: "Preferences" }),
  ).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
  await expectAccessible(page);
});

test("Preferences_Arabic_IsRightToLeft", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.ar);

  await page.goto(`/${northwind.slug}/settings/preferences`);

  await expect(
    page.getByRole("heading", { level: 1, name: "التفضيلات" }),
  ).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await expectAccessible(page);
});
