import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import {
  type ChangePreferencesRequest,
  Language,
  type RenameMeRequest,
} from "@repo/api-client";
import { mockApi, mockPreferences, mockRename, northwind } from "./api";
import { expectAccessible } from "./axe";

test("Profile_English_IsLeftToRight", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.En);

  await page.goto(`/${northwind.slug}/settings/profile`);

  await expect(
    page.getByRole("heading", { level: 1, name: "Profile" }),
  ).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
  await expectAccessible(page);
});

test("Profile_Arabic_IsRightToLeft", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.Ar);

  await page.goto(`/${northwind.slug}/settings/profile`);

  await expect(
    page.getByRole("heading", { level: 1, name: "الملف الشخصي" }),
  ).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await expectAccessible(page);
});

test("Profile_ArabicForm_SavesTheTimeZone", async ({ page }) => {
  const bodies: ChangePreferencesRequest[] = [];
  await setupClerkTestingToken({ page });
  await mockPreferences(page, Language.Ar, bodies);

  await page.goto(`/${northwind.slug}/settings/profile`);
  const language = page.getByRole("combobox", { name: "اللغة" });
  await expect(language).toHaveText("العربية");
  await expectAccessible(page);
  await page.getByRole("combobox", { name: "المنطقة الزمنية" }).click();
  await page
    .getByRole("combobox", { name: "ابحث عن مدينة أو منطقة" })
    .fill("Riyadh");
  await page.getByRole("option", { name: /^Riyadh/ }).click();
  await page.getByRole("button", { name: "حفظ التغييرات" }).click();

  await expect(page.getByText("تم حفظ الملف الشخصي")).toBeVisible();
  expect(bodies).toEqual([{ language: Language.Ar, timeZone: "Asia/Riyadh" }]);
  await expectAccessible(page);
});

test("Profile_Rename_ShowsTheNewNameInTheUserMenu", async ({ page }) => {
  const bodies: RenameMeRequest[] = [];
  await setupClerkTestingToken({ page });
  await mockRename(page, Language.En, bodies);

  await page.goto(`/${northwind.slug}/settings/profile`);
  await expect(page.getByRole("button", { name: /Ayşe Yılmaz/ })).toBeVisible();
  await page.getByRole("textbox", { name: "Name" }).fill("Ayla Kaya");
  await page.getByRole("button", { name: "Save changes" }).click();

  await expect(page.getByText("Profile saved")).toBeVisible();
  await expectAccessible(page);
  expect(bodies).toEqual([{ name: "Ayla Kaya" }]);
  await page.getByRole("button", { name: /Ayla Kaya/ }).click();
  await expect(page.getByRole("menu").getByText("Ayla Kaya")).toBeVisible();
});
