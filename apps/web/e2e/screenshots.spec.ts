import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { Language, MemberRole } from "@repo/api-client";
import { railCookieName } from "@repo/ui/lib/rail-state";
import { mockApi, mockMembers, northwind } from "./api";

// Screenshots for a pull request. They run only with E2E_SCREENSHOTS=1 and write to apps/web/test-results/screenshots,
// which Git ignores and Playwright empties at the start of each run. Attach them to the pull request; never commit them.
test.skip(
  !process.env.E2E_SCREENSHOTS,
  "Set E2E_SCREENSHOTS=1 to take the screenshots.",
);

const folder = "test-results/screenshots";

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

const members = `/${northwind.slug}/settings/members`;
const shots: {
  name: string;
  role: MemberRole;
  language: Language;
  scheme: "light" | "dark";
  width: number;
  height: number;
  query?: string;
  rail?: boolean;
}[] = [
  {
    name: "members-owner-en-light-desktop",
    role: MemberRole.Owner,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
  },
  {
    name: "members-owner-en-light-desktop-rail",
    role: MemberRole.Owner,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
    rail: true,
  },
  {
    name: "members-admin-en-dark-desktop",
    role: MemberRole.Admin,
    language: Language.En,
    scheme: "dark",
    width: 1440,
    height: 900,
  },
  {
    name: "members-member-tr-light-tablet",
    role: MemberRole.Member,
    language: Language.Tr,
    scheme: "light",
    width: 834,
    height: 1112,
  },
  {
    name: "members-owner-ar-dark-desktop-rail",
    role: MemberRole.Owner,
    language: Language.Ar,
    scheme: "dark",
    width: 1440,
    height: 900,
    rail: true,
  },
  {
    name: "members-owner-ar-light-phone",
    role: MemberRole.Owner,
    language: Language.Ar,
    scheme: "light",
    width: 390,
    height: 844,
  },
  {
    name: "invitations-owner-en-light-desktop",
    role: MemberRole.Owner,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
    query: "?tab=invitations",
  },
  {
    name: "invitations-owner-tr-dark-phone",
    role: MemberRole.Owner,
    language: Language.Tr,
    scheme: "dark",
    width: 390,
    height: 844,
    query: "?tab=invitations",
  },
];

for (const shot of shots) {
  test(`Screenshot_${shot.name}`, async ({ page, context, baseURL }) => {
    await setupClerkTestingToken({ page });
    await page.emulateMedia({ colorScheme: shot.scheme });
    await page.setViewportSize({ width: shot.width, height: shot.height });
    await context.addCookies([
      { name: railCookieName, value: String(shot.rail === true), url: baseURL },
    ]);
    await mockMembers(page, { role: shot.role, language: shot.language });

    await page.goto(`${members}${shot.query ?? ""}`);
    await page.getByRole("heading", { level: 1 }).waitFor();
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);

    await page.screenshot({
      path: `${folder}/${shot.name}.png`,
      fullPage: true,
    });
  });
}
