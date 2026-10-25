import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import {
  BuiltInRole,
  Language,
  MyTenantResponseStatus,
} from "@repo/api-client";
import { localeCookie } from "@repo/ui/lib/locale";
import { railCookieName } from "@repo/ui/lib/rail-state";
import {
  roles as builtInRoles,
  customRoles,
  me,
  mockApi,
  mockAuditLog,
  mockMembers,
  mockTenantStatus,
  northwind,
} from "./api";

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

/** Opens a row's ⋯ menu and picks an item, so the dialog shows. */
const rowAction = (menu: string, item: string) => async (page: Page) => {
  await page.getByRole("button", { name: menu }).click();
  await page.getByRole("menuitem", { name: item }).click();
  await page.getByRole("alertdialog").waitFor();
};
/** Opens the user menu and waits until it shows. */
const openUserMenu = (name: string) => async (page: Page) => {
  await page.getByRole("button", { name }).click();
  await expect(page.getByRole("menu")).toBeVisible();
};
const shots: {
  name: string;
  file: string;
  role: BuiltInRole;
  language: Language;
  scheme: "light" | "dark";
  width: number;
  height: number;
  query: string;
  rail: boolean;
  /** A step after the page loads: open a dialog or a menu. */
  open?: (page: Page) => Promise<void>;
}[] = [
  {
    name: "Members_OwnerEnglishLightDesktop",
    file: "members-owner-en-light-desktop",
    role: BuiltInRole.Owner,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
    query: "",
    rail: false,
  },
  {
    name: "Members_OwnerEnglishLightDesktopRailExpanded",
    file: "members-owner-en-light-desktop-rail",
    role: BuiltInRole.Owner,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
    rail: true,
    query: "",
  },
  {
    name: "Members_AdminEnglishDarkDesktop",
    file: "members-admin-en-dark-desktop",
    role: BuiltInRole.Admin,
    language: Language.En,
    scheme: "dark",
    width: 1440,
    height: 900,
    query: "",
    rail: false,
  },
  {
    name: "Members_MemberTurkishLightTablet",
    file: "members-member-tr-light-tablet",
    role: BuiltInRole.Member,
    language: Language.Tr,
    scheme: "light",
    width: 834,
    height: 1112,
    query: "",
    rail: false,
  },
  {
    name: "Members_OwnerArabicDarkDesktopRailExpanded",
    file: "members-owner-ar-dark-desktop-rail",
    role: BuiltInRole.Owner,
    language: Language.Ar,
    scheme: "dark",
    width: 1440,
    height: 900,
    rail: true,
    query: "",
  },
  {
    name: "Members_OwnerArabicLightPhone",
    file: "members-owner-ar-light-phone",
    role: BuiltInRole.Owner,
    language: Language.Ar,
    scheme: "light",
    width: 390,
    height: 844,
    query: "",
    rail: false,
  },
  {
    name: "Invitations_OwnerEnglishLightDesktop",
    file: "invitations-owner-en-light-desktop",
    role: BuiltInRole.Owner,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
    query: "?tab=invitations",
    rail: false,
  },
  {
    name: "Invitations_OwnerTurkishDarkPhone",
    file: "invitations-owner-tr-dark-phone",
    role: BuiltInRole.Owner,
    language: Language.Tr,
    scheme: "dark",
    width: 390,
    height: 844,
    query: "?tab=invitations",
    rail: false,
  },
  {
    name: "ChangeRole_AdminEnglishLightDesktop",
    file: "change-role-admin-en-light-desktop",
    role: BuiltInRole.Admin,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
    query: "",
    rail: false,
    open: rowAction("Actions for John Byrne", "Change role"),
  },
  {
    name: "ChangeRole_AdminArabicDarkDesktop",
    file: "change-role-admin-ar-dark-desktop",
    role: BuiltInRole.Admin,
    language: Language.Ar,
    scheme: "dark",
    width: 1440,
    height: 900,
    query: "",
    rail: false,
    open: rowAction("إجراءات John Byrne", "تغيير الدور"),
  },
  {
    name: "ChangeRole_AdminEnglishLightPhone",
    file: "change-role-admin-en-light-phone",
    role: BuiltInRole.Admin,
    language: Language.En,
    scheme: "light",
    width: 390,
    height: 844,
    query: "",
    rail: false,
    open: rowAction("Actions for John Byrne", "Change role"),
  },
  {
    name: "MembersMenu_AdminOnBillingLeadEnglishLightDesktop",
    file: "members-menu-admin-en-light-desktop",
    role: BuiltInRole.Admin,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
    query: "",
    rail: false,
    open: (page) =>
      page.getByRole("button", { name: "Actions for Dara Quinn" }).click(),
  },
  {
    name: "Invite_AdminEnglishDarkDesktop",
    file: "invite-admin-en-dark-desktop",
    role: BuiltInRole.Admin,
    language: Language.En,
    scheme: "dark",
    width: 1440,
    height: 900,
    query: "?tab=invitations",
    rail: false,
    open: (page) => page.getByRole("button", { name: "Invite member" }).click(),
  },
  {
    name: "Invite_AdminTurkishLightPhone",
    file: "invite-admin-tr-light-phone",
    role: BuiltInRole.Admin,
    language: Language.Tr,
    scheme: "light",
    width: 390,
    height: 844,
    query: "?tab=invitations",
    rail: false,
    open: (page) => page.getByRole("button", { name: "Üye davet et" }).click(),
  },
  {
    name: "InvitationsMenu_AdminOnBillingLeadArabicLightDesktop",
    file: "invitations-menu-admin-ar-light-desktop",
    role: BuiltInRole.Admin,
    language: Language.Ar,
    scheme: "light",
    width: 1440,
    height: 900,
    query: "?tab=invitations",
    rail: false,
    open: (page) =>
      page.getByRole("button", { name: "إجراءات niall@northwind.io" }).click(),
  },
  {
    name: "UserMenu_OwnerEnglishLightDesktop",
    file: "user-menu-owner-en-light-desktop",
    role: BuiltInRole.Owner,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
    query: "",
    rail: false,
    open: openUserMenu("AY Account"),
  },
  {
    name: "UserMenu_OwnerArabicDarkDesktop",
    file: "user-menu-owner-ar-dark-desktop",
    role: BuiltInRole.Owner,
    language: Language.Ar,
    scheme: "dark",
    width: 1440,
    height: 900,
    query: "",
    rail: false,
    open: openUserMenu("AY الحساب"),
  },
  {
    name: "UserMenu_OwnerEnglishLightPhone",
    file: "user-menu-owner-en-light-phone",
    role: BuiltInRole.Owner,
    language: Language.En,
    scheme: "light",
    width: 390,
    height: 844,
    query: "",
    rail: false,
    open: openUserMenu("AY Account"),
  },
  {
    name: "MembersByRole_OwnerEnglishLightDesktop",
    file: "members-by-role-owner-en-light-desktop",
    role: BuiltInRole.Owner,
    language: Language.En,
    scheme: "light",
    width: 1440,
    height: 900,
    query: "?roleId=0198f0a2-0000-7000-8000-0000000000c1",
    rail: false,
  },
];

for (const shot of shots) {
  test(`Screenshot_${shot.name}`, async ({ page, context, baseURL }) => {
    await setupClerkTestingToken({ page });
    await page.emulateMedia({ colorScheme: shot.scheme });
    await page.setViewportSize({ width: shot.width, height: shot.height });
    await context.addCookies([
      { name: railCookieName, value: String(shot.rail), url: baseURL },
    ]);
    await mockMembers(page, { role: shot.role, language: shot.language });

    await page.goto(`${members}${shot.query}`);
    await page.getByRole("heading", { level: 1 }).waitFor();
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
    await shot.open?.(page);

    await page.screenshot({
      path: `${folder}/${shot.file}.png`,
      fullPage: true,
    });
  });
}

// The pages of step 21d, in each language, both themes and the three widths.
const settings = `/${northwind.slug}/settings`;
const sizes = {
  desktop: { width: 1440, height: 900 },
  tablet: { width: 834, height: 1112 },
  phone: { width: 390, height: 844 },
};
const variants = [
  { language: Language.En, scheme: "light", size: "desktop" },
  { language: Language.Tr, scheme: "dark", size: "tablet" },
  { language: Language.Ar, scheme: "light", size: "phone" },
  { language: Language.Ar, scheme: "dark", size: "desktop" },
] as const;

/** The viewer of the role reads the tenant log, in the language. */
const viewer =
  (language: Language, role: BuiltInRole) => async (page: Page) => {
    await mockAuditLog(page, { role });
    await page.route("**/api/v1/me", (route) =>
      route.fulfill({ json: me(language) }),
    );
  };

const pages = variants.flatMap(({ language, scheme, size }) =>
  [
    ["general", `${settings}/general`, viewer(language, BuiltInRole.Owner)],
    ["activity", `${settings}/activity`, viewer(language, BuiltInRole.Owner)],
    [
      "activity-member",
      `${settings}/activity`,
      viewer(language, BuiltInRole.Member),
    ],
    [
      "preferences",
      `${settings}/preferences`,
      (page: Page) => mockApi(page, language),
    ],
    ["overview", `/${northwind.slug}`, (page: Page) => mockApi(page, language)],
    [
      "suspended",
      `/${northwind.slug}`,
      (page: Page) => mockTenantStatus(page, MyTenantResponseStatus.Suspended),
    ],
    [
      "closed",
      `/${northwind.slug}`,
      (page: Page) => mockTenantStatus(page, MyTenantResponseStatus.Closed),
    ],
  ].map(([name, path, mock]) => ({
    file: `${name}-${language}-${scheme}-${size}`,
    path: path as string,
    language,
    scheme,
    size,
    mock: mock as (page: Page) => Promise<void>,
  })),
);

for (const shot of pages) {
  test(`Screenshot_${shot.file}`, async ({ page, context, baseURL }) => {
    await setupClerkTestingToken({ page });
    await page.emulateMedia({ colorScheme: shot.scheme });
    await page.setViewportSize(sizes[shot.size]);
    // The language's cookie (F13), so the first render is already in it. The blocked pages read no GetMe.
    await context.addCookies([
      { name: localeCookie, value: shot.language, url: baseURL },
    ]);
    await shot.mock(page);

    await page.goto(shot.path);
    await page.getByRole("heading", { level: 1 }).waitFor();
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);

    await page.screenshot({
      path: `${folder}/${shot.file}.png`,
      fullPage: true,
    });
  });
}

// The roles pages of step 22i, in the four variants of a review: English light and dark on a desktop, Turkish on a tablet and
// Arabic on a phone.
const roles = `${settings}/roles`;
const reviewVariants = [
  { language: Language.En, scheme: "light", size: "desktop" },
  { language: Language.En, scheme: "dark", size: "desktop" },
  { language: Language.Tr, scheme: "light", size: "tablet" },
  { language: Language.Ar, scheme: "light", size: "phone" },
] as const;
const rolePages = [
  ["roles", roles],
  ["new-role", `${roles}/new`],
  ["custom-role", `${roles}/${customRoles[0]?.id}`],
  ["built-in-role", `${roles}/${builtInRoles.admin.id}`],
] as const;

for (const { language, scheme, size } of reviewVariants)
  for (const [name, path] of rolePages)
    test(`Screenshot_${name}-${language}-${scheme}-${size}`, async ({
      page,
      context,
      baseURL,
    }) => {
      await setupClerkTestingToken({ page });
      await page.emulateMedia({ colorScheme: scheme });
      await page.setViewportSize(sizes[size]);
      await context.addCookies([
        { name: localeCookie, value: language, url: baseURL },
      ]);
      await mockMembers(page, { language });

      await page.goto(path);
      await page.getByRole("heading", { level: 1 }).waitFor();
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);

      await page.screenshot({
        path: `${folder}/${name}-${language}-${scheme}-${size}.png`,
        fullPage: true,
      });
    });
