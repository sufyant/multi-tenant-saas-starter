import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import {
  BuiltInRole,
  Language,
  MyTenantResponseStatus,
} from "@repo/api-client";
import { localeCookie } from "@repo/ui/lib/locale";
import {
  roles as builtInRoles,
  customRoles,
  me,
  mockApi,
  mockAuditLog,
  mockMembers,
  mockNotifications,
  mockTenantStatus,
  northwind,
  type Plan,
  pro,
  roleChanged,
} from "./api";

/** The bell's tabs; a page may have tabs of its own. */
const bellTabs = (page: Page) =>
  page.getByRole("tablist", { name: "Which notifications" });

// Screenshots for a pull request. They run only with E2E_SCREENSHOTS=1 and write to apps/web/test-results/screenshots,
// which Git ignores and Playwright empties at the start of each run. Attach them to the pull request; never commit them.
test.skip(
  !process.env.E2E_SCREENSHOTS,
  "Set E2E_SCREENSHOTS=1 to take the screenshots.",
);

const folder = "test-results/screenshots";
// CI runs the production build, which has no /dev/components (F33): its shots run on a development server only.
const productionBuild = Boolean(process.env.CI);

test("Screenshot_DevComponents_EnglishLightDesktop", async ({ page }) => {
  test.skip(productionBuild, "A production build has no /dev/components.");
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
  test.skip(productionBuild, "A production build has no /dev/components.");
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

test("Screenshot_Profile_TurkishLightTablet", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "light" });
  await page.setViewportSize({ width: 834, height: 1112 });
  await mockApi(page, Language.Tr);

  await page.goto(`/${northwind.slug}/settings/profile`);
  await page
    .getByRole("main")
    .getByRole("heading", { level: 1, name: "Profil" })
    .waitFor();

  await page.screenshot({ path: `${folder}/profile-tr-light-tablet.png` });
});

const members = `/${northwind.slug}/settings/members`;

/** Opens a row's ⋯ menu and picks an item, so the dialog shows. */
const rowAction = (menu: string, item: string) => async (page: Page) => {
  await page.getByRole("button", { name: menu }).click();
  await page.getByRole("menuitem", { name: item }).click();
  await page.getByRole("alertdialog").waitFor();
};
/** Opens the account menu, from the phone's drawer on a phone, and waits until it shows. */
const openUserMenu = (name: string) => async (page: Page) => {
  const drawer = page.getByRole("button", { name: "Open menu" });
  if (await drawer.isVisible()) await drawer.click();
  await page.getByRole("button", { name, exact: true }).click();
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
    open: openUserMenu("Ayşe Yılmaz"),
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
    open: openUserMenu("Ayşe Yılmaz"),
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
    open: openUserMenu("Ayşe Yılmaz"),
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
  },
];

for (const shot of shots) {
  test(`Screenshot_${shot.name}`, async ({ page }) => {
    await setupClerkTestingToken({ page });
    await page.emulateMedia({ colorScheme: shot.scheme });
    await page.setViewportSize({ width: shot.width, height: shot.height });
    await mockMembers(page, { role: shot.role, language: shot.language });

    await page.goto(`${members}${shot.query}`);
    await page.getByRole("main").getByRole("heading").first().waitFor();
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
    ["profile", `${settings}/profile`, (page: Page) => mockApi(page, language)],
    ["home", `/${northwind.slug}`, (page: Page) => mockApi(page, language)],
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
    await page.getByRole("main").getByRole("heading").first().waitFor();
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
      await page.getByRole("main").getByRole("heading").first().waitFor();
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);

      await page.screenshot({
        path: `${folder}/${name}-${language}-${scheme}-${size}.png`,
        fullPage: true,
      });
    });

// The plan-limit pages of step 32-frontend-2, in the four variants of a review.
const planWith = (
  entitlements: Plan["entitlements"],
  usage: Plan["usage"],
): Plan => ({
  planName: "Free",
  entitlements: { ...pro.entitlements, ...entitlements },
  usage: { ...pro.usage, ...usage },
});
const customRolesOff = planWith(
  { "roles.custom": false, "roles.custom.max": 0 },
  {},
);
const planPages: {
  name: string;
  path: string;
  plan: Plan;
  role?: BuiltInRole;
  open?: (page: Page) => Promise<void>;
}[] = [
  { name: "roles-plan-off", path: roles, plan: customRolesOff },
  {
    name: "roles-plan-over",
    path: roles,
    plan: planWith({ "roles.custom.max": 1 }, {}),
  },
  {
    name: "members-plan-limit",
    path: members,
    plan: planWith({ "members.max": 10 }, { "members.max": 10 }),
  },
  {
    name: "members-plan-over",
    path: members,
    plan: planWith({ "members.max": 5 }, { "members.max": 10 }),
  },
  {
    name: "members-plan-over-member",
    path: members,
    plan: planWith({ "members.max": 5 }, { "members.max": 10 }),
    role: BuiltInRole.Member,
  },
  {
    name: "change-role-plan-off",
    path: members,
    plan: customRolesOff,
    // In every language: John Byrne's ⋯ menu, then its first item, Change role. The menu is in the table, or in his card on a
    // phone, where the table is hidden.
    open: async (page) => {
      await page
        .getByRole("button", { name: /John Byrne/ })
        .filter({ visible: true })
        .click();
      await page.getByRole("menuitem").first().click();
      await page.getByRole("alertdialog").waitFor();
    },
  },
  {
    name: "invite-plan-off",
    path: members,
    plan: customRolesOff,
    open: async (page) => {
      await page.getByRole("button", { name: /invite|davet|دعوة/i }).click();
      await page.getByRole("alertdialog").waitFor();
    },
  },
  { name: "general-plan", path: `${settings}/general`, plan: pro },
];

for (const { language, scheme, size } of reviewVariants)
  for (const shot of planPages)
    test(`Screenshot_${shot.name}-${language}-${scheme}-${size}`, async ({
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
      await mockMembers(page, { language, role: shot.role, plan: shot.plan });

      await page.goto(shot.path);
      await page.getByRole("main").getByRole("heading").first().waitFor();
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
      await shot.open?.(page);

      await page.screenshot({
        path: `${folder}/${shot.name}-${language}-${scheme}-${size}.png`,
        fullPage: true,
      });
    });

// The bell and its list (the Notifications boards): the popover on a desktop, the full screen on a phone in Arabic, and
// Settings › Notifications on a tablet in Turkish.
test("Screenshot_Notifications_EnglishLightDesktop", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "light" });
  await page.setViewportSize({ width: 1440, height: 900 });
  await mockApi(page);
  await mockNotifications(page);

  await page.goto(`/${northwind.slug}`);
  await page.getByRole("button", { name: "Notifications, 1 unread" }).click();
  await page.getByRole("link", { name: /Your role/ }).waitFor();

  await page.screenshot({
    path: `${folder}/notifications-en-light-desktop.png`,
  });
});

test("Screenshot_Notifications_ArabicDarkPhone", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "dark" });
  await page.setViewportSize({ width: 390, height: 844 });
  await mockApi(page, Language.Ar);
  await mockNotifications(page);

  await page.goto(`/${northwind.slug}`);
  await page.getByRole("button", { name: /الإشعارات/ }).click();
  await page.getByRole("dialog").waitFor();

  await page.screenshot({ path: `${folder}/notifications-ar-dark-phone.png` });
});

// The shell at the sizes of the design's frames (the Shell, Notifications and Home screens), for the comparison in each pull
// request: CI uploads them as the workflow artifact "screenshots" (scripts/ci/test-e2e.sh). The name gives the frame.
const frames = {
  desktop: { width: 1280, height: 800 },
  tablet: { width: 1024, height: 768 },
  phone: { width: 375, height: 812 },
};
const blueHarbour = {
  ...northwind,
  id: "0198f0a2-0000-7000-8000-0000000000b2",
  slug: "blue-harbour",
  name: "Blue Harbour Logistics",
  status: MyTenantResponseStatus.Suspended,
};

/**
 * The Notifications frames' list: two unread items of the last hour, one read item of yesterday and a personal one about no
 * organisation, each with its area.
 */
const ago = (minutes: number) =>
  new Date(Date.now() - minutes * 60_000).toISOString();
const bellList = {
  unread: 2,
  items: [
    {
      ...roleChanged,
      id: "0198f0a2-0000-7000-8000-0000000000a3",
      title: "Omar Haddad accepted your invitation",
      area: "Members",
      actorName: "Omar Haddad",
      createdAt: ago(2),
    },
    { ...roleChanged, createdAt: ago(60) },
    {
      ...roleChanged,
      id: "0198f0a2-0000-7000-8000-0000000000a4",
      title: "The plan of Northwind Traders changed to Team",
      area: "Organisation plan",
      createdAt: ago(24 * 60),
      readAt: ago(60),
      unread: false,
    },
    {
      ...roleChanged,
      id: "0198f0a2-0000-7000-8000-0000000000a5",
      title: "You were removed from Atlas Logistics",
      area: "Your access",
      tenantName: null,
      createdAt: ago(3 * 24 * 60),
      readAt: ago(2 * 24 * 60),
      unread: false,
    },
  ],
};
const openBell = (page: Page) =>
  page.getByRole("button", { name: /^Notifications/ }).click();
/** On a phone the bell opens its full screen, which slides up after the frames the shot waits for: wait until it reaches the top. */
const openBellFullScreen = async (page: Page) => {
  await openBell(page);
  await expect
    .poll(async () => (await page.getByRole("dialog").boundingBox())?.y)
    .toBeLessThanOrEqual(0);
};

/** Scrolls the theme's card, the one radio group of the Profile page, into view. */
const showTheme = (page: Page) =>
  page.getByRole("radiogroup").scrollIntoViewIfNeeded();

/** Waits until Account and security has read the sessions from Clerk: the current one carries "This device". */
const sessionsShown = (page: Page) =>
  page.getByText("This device", { exact: true }).waitFor();

const shellShots: {
  file: string;
  size: keyof typeof frames;
  language?: Language;
  scheme?: "light" | "dark";
  role?: BuiltInRole;
  path?: string;
  mock?: (page: Page) => Promise<unknown>;
  open?: (page: Page) => Promise<void>;
}[] = [
  { file: "2.01-home-en-light-desktop", size: "desktop" },
  {
    file: "4.02-home-member-en-light-desktop",
    size: "desktop",
    role: BuiltInRole.Member,
  },
  { file: "2.28-home-en-dark-desktop", size: "desktop", scheme: "dark" },
  {
    file: "2.01-home-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
  },
  { file: "3.13-home-en-light-tablet", size: "tablet" },
  { file: "2.29-home-en-light-phone", size: "phone" },
  {
    file: "2.29-drawer-en-light-phone",
    size: "phone",
    open: (page) => page.getByRole("button", { name: "Open menu" }).click(),
  },
  {
    file: "2.29-drawer-ar-light-phone",
    size: "phone",
    language: Language.Ar,
    open: (page) => page.getByRole("button", { name: "فتح القائمة" }).click(),
  },
  {
    file: "2.03-switcher-en-light-desktop",
    size: "desktop",
    mock: (page) =>
      page.route("**/api/v1/me/tenants*", (route) =>
        route.fulfill({
          json: {
            items: [northwind, blueHarbour],
            page: 1,
            pageSize: 100,
            total: 2,
          },
        }),
      ),
    open: (page) =>
      page.getByRole("button", { name: /Northwind Traders/ }).click(),
  },
  {
    file: "2.05-command-en-light-desktop",
    size: "desktop",
    open: (page) => page.keyboard.press("ControlOrMeta+k"),
  },
  {
    file: "2.06-account-menu-en-light-desktop",
    size: "desktop",
    open: (page) =>
      page.getByRole("button", { name: "Ayşe Yılmaz", exact: true }).click(),
  },
  {
    file: "2.07-notifications-en-light-desktop",
    size: "desktop",
    mock: mockNotifications,
    open: (page) =>
      page.getByRole("button", { name: /^Notifications/ }).click(),
  },
  {
    file: "3.01-notifications-unread-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, bellList),
    open: openBell,
  },
  {
    file: "3.02-notifications-all-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, bellList),
    open: async (page) => {
      await openBell(page);
      await bellTabs(page).getByRole("tab", { name: "All" }).click();
      await page.getByRole("link", { name: /You were removed/ }).waitFor();
    },
  },
  {
    file: "3.03-notifications-empty-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, { items: [], unread: 0 }),
    open: openBell,
  },
  {
    file: "3.16-notifications-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    mock: (page) => mockNotifications(page, bellList),
    open: openBell,
  },
  {
    file: "3.14-notifications-en-light-phone",
    size: "phone",
    mock: (page) => mockNotifications(page, bellList),
    open: openBellFullScreen,
  },
  {
    file: "3.15-notifications-empty-en-light-phone",
    size: "phone",
    mock: (page) => mockNotifications(page, { items: [], unread: 0 }),
    open: openBellFullScreen,
  },
  {
    file: "5-settings-members-en-light-desktop",
    size: "desktop",
    path: `${settings}/members`,
  },
  {
    file: "5-settings-members-en-light-tablet",
    size: "tablet",
    path: `${settings}/members`,
  },
  {
    file: "2.30-settings-drawer-en-light-phone",
    size: "phone",
    path: `${settings}/members`,
    open: (page) => page.getByRole("button", { name: "Open menu" }).click(),
  },
  {
    file: "2.40-not-found-en-light-desktop",
    size: "desktop",
    path: `/${northwind.slug}/no-such-page`,
  },
  {
    file: "2.43-not-found-en-light-phone",
    size: "phone",
    path: `/${northwind.slug}/no-such-page`,
  },
  {
    file: "2.15-no-permission-en-light-desktop",
    size: "desktop",
    role: BuiltInRole.Member,
    path: `${settings}/activity`,
  },
  // The theme's card under the Profile form (36-design): System on a light device, Dark chosen in the dark frame. Each shot
  // scrolls the card into view, because the two name fields make the form taller than the frame's.
  {
    file: "5.01-profile-en-light-desktop",
    size: "desktop",
    path: `${settings}/profile`,
    open: showTheme,
  },
  {
    file: "5.08-profile-en-light-tablet",
    size: "tablet",
    path: `${settings}/profile`,
    open: showTheme,
  },
  {
    file: "5.09-profile-en-light-phone",
    size: "phone",
    path: `${settings}/profile`,
    open: showTheme,
  },
  {
    file: "5.07-profile-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: `${settings}/profile`,
    open: async (page) => {
      await page.getByRole("radio", { name: "Dark" }).click();
      await showTheme(page);
    },
  },
  {
    file: "5.10-profile-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: `${settings}/profile`,
    open: showTheme,
  },
  // The Dates and times pattern: a time's line on hover, the city and its offset (F18, F60).
  {
    file: "pattern-time-en-light-desktop",
    size: "desktop",
    path: `${settings}/activity`,
    mock: viewer(Language.En, BuiltInRole.Owner),
    open: async (page) => {
      await page.getByRole("main").locator("time").first().hover();
      // The tooltip's line ends with the zone's city and offset; the shot waits for that text, as time.test.tsx does.
      await page.getByText(/ · .+ \(UTC[+-]\d+(:\d+)?\)$/).waitFor();
    },
  },
  // Account and security (F62). Clerk is the real development instance: the test user has this one session and no linked
  // account, the state of 6.05.
  {
    file: "6.05-account-en-light-desktop",
    size: "desktop",
    path: `${settings}/account`,
    open: sessionsShown,
  },
  {
    file: "6.06-account-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: `${settings}/account`,
    open: sessionsShown,
  },
  {
    file: "6.07-account-en-light-phone",
    size: "phone",
    path: `${settings}/account`,
    open: sessionsShown,
  },
];

/** A shot's language, theme, role and page when it names none: English, light, the owner, the start page. */
const withDefaults = (shot: (typeof shellShots)[number]) => ({
  ...shot,
  language: shot.language ?? Language.En,
  scheme: shot.scheme ?? "light",
  role: shot.role ?? BuiltInRole.Owner,
  path: shot.path ?? `/${northwind.slug}`,
});

for (const shot of shellShots.map(withDefaults))
  test(`Screenshot_Shell_${shot.file}`, async ({ page, context, baseURL }) => {
    await setupClerkTestingToken({ page });
    await page.emulateMedia({ colorScheme: shot.scheme });
    await page.setViewportSize(frames[shot.size]);
    await context.addCookies([
      { name: localeCookie, value: shot.language, url: baseURL },
    ]);
    await mockMembers(page, { language: shot.language, role: shot.role });
    await shot.mock?.(page);

    await page.goto(shot.path);
    await page.getByRole("main").getByRole("heading").first().waitFor();
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
    await shot.open?.(page);
    // The screen as it rests: every opening animation has finished. A popup mounts a frame or two after the click, so its
    // animation starts only then.
    await page.evaluate(
      () =>
        new Promise((done) =>
          requestAnimationFrame(() => requestAnimationFrame(done)),
        ),
    );
    await page.evaluate(() =>
      Promise.all(document.getAnimations().map((each) => each.finished)),
    );

    await page.screenshot({ path: `${folder}/shell-${shot.file}.png` });
  });
