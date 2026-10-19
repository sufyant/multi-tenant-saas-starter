import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { test } from "@playwright/test";
import { Language } from "@repo/api-client";
import { mockApi, northwind } from "./api";

// The screenshots of the pull request of step 21a. They run only with E2E_SCREENSHOTS=1 and write to docs/frontend/screenshots.
test.skip(
  !process.env.E2E_SCREENSHOTS,
  "Set E2E_SCREENSHOTS=1 to take the screenshots.",
);

const folder = "../../docs/frontend/screenshots";

test("Screenshot_DevComponents_EnglishLightDesktop", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "light" });
  await page.setViewportSize({ width: 1440, height: 900 });

  await page.goto("/dev/components");
  await page.getByRole("region", { name: "AppShell" }).waitFor();

  await page.screenshot({
    path: `${folder}/dev-components-en-light-desktop.png`,
    fullPage: true,
  });
});

test("Screenshot_DevComponents_ArabicDarkPhone", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "dark" });
  await page.setViewportSize({ width: 390, height: 844 });

  await page.goto("/dev/components");
  await page.getByRole("button", { name: "ar", exact: true }).click();
  await page.getByRole("button", { name: "dark", exact: true }).click();
  await page.getByRole("region", { name: "AppShell" }).waitFor();

  await page.screenshot({
    path: `${folder}/dev-components-ar-dark-phone.png`,
    fullPage: true,
  });
});

test("Screenshot_Preferences_TurkishLightTablet", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "light" });
  await page.setViewportSize({ width: 834, height: 1112 });
  await mockApi(page, Language.Tr);

  await page.goto(`/${northwind.slug}/settings/preferences`);
  await page.getByRole("heading", { level: 1, name: "Tercihler" }).waitFor();

  await page.screenshot({ path: `${folder}/preferences-tr-light-tablet.png` });
});
