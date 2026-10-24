import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import {
  type ChangePreferencesRequest,
  Language,
  type RenameMeRequest,
} from "@repo/api-client";
import { mockApi, mockPreferences, mockRename, northwind } from "./api";
import { expectAccessible } from "./axe";

test("Preferences_English_IsLeftToRight", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.En);

  await page.goto(`/${northwind.slug}/settings/preferences`);

  await expect(
    page.getByRole("heading", { level: 1, name: "Preferences" }),
  ).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
  await expectAccessible(page);
});

test("Preferences_Arabic_IsRightToLeft", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.Ar);

  await page.goto(`/${northwind.slug}/settings/preferences`);

  await expect(
    page.getByRole("heading", { level: 1, name: "التفضيلات" }),
  ).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await expectAccessible(page);
});

test("Preferences_ArabicForm_SavesTheTimeZone", async ({ page }) => {
  const bodies: ChangePreferencesRequest[] = [];
  await setupClerkTestingToken({ page });
  await mockPreferences(page, Language.Ar, bodies);

  await page.goto(`/${northwind.slug}/settings/preferences`);
  const language = page.getByRole("combobox", { name: "اللغة" });
  await expect(language).toHaveText("العربية");
  await expectAccessible(page);
  await page.getByRole("combobox", { name: "المنطقة الزمنية" }).click();
  await page
    .getByRole("combobox", { name: "ابحث عن مدينة أو منطقة" })
    .fill("Riyadh");
  await page.getByRole("option", { name: /^Riyadh/ }).click();
  await page.getByRole("button", { name: "حفظ التفضيلات" }).click();

  await expect(page.getByText("تم حفظ التفضيلات")).toBeVisible();
  expect(bodies).toEqual([{ language: Language.Ar, timeZone: "Asia/Riyadh" }]);
  await expectAccessible(page);
});

test("Preferences_Rename_ShowsTheNewNameInTheUserMenu", async ({ page }) => {
  const bodies: RenameMeRequest[] = [];
  await setupClerkTestingToken({ page });
  await mockRename(page, Language.En, bodies);

  await page.goto(`/${northwind.slug}/settings/preferences`);
  await expect(page.getByRole("button", { name: "AY Account" })).toBeVisible();
  await page.getByRole("textbox", { name: "First name" }).fill("Ayla");
  await page.getByRole("textbox", { name: "Last name" }).fill("Kaya");
  await page.getByRole("button", { name: "Save preferences" }).click();

  await expect(page.getByText("Preferences saved")).toBeVisible();
  expect(bodies).toEqual([{ firstName: "Ayla", lastName: "Kaya" }]);
  await page.getByRole("button", { name: "AK Account" }).click();
  await expect(page.getByText("Ayla Kaya")).toBeVisible();
  await expectAccessible(page);
});
