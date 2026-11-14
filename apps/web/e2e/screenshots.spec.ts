import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, type Route, test } from "@playwright/test";
import {
  BuiltInRole,
  DataExportResponseStatus,
  ErrorCode,
  InvitationResponseState,
  Language,
  MyTenantResponseStatus,
  MyTermsResponseAction,
  type Permission,
  type RoleDetailResponse,
} from "@repo/api-client";
import { localeCookie } from "@repo/ui/lib/locale";
import { activityFileCap } from "../src/features/activity/file-cap";
import { lastOrganisationCookie } from "../src/shared/last-organisation-cookie";
import {
  roles as builtInRoles,
  customRoles,
  decemberTerms,
  invitationCode,
  manyMembers,
  me,
  mockActivityExport,
  mockActivityFrame,
  mockApi,
  mockAuditLog,
  mockDataExport,
  mockInvitation,
  mockMembers,
  mockMembersExport,
  mockMyTerms,
  mockNotifications,
  mockRename,
  mockRetention,
  mockTenantStatus,
  northwind,
  northwindTeam,
  type Plan,
  pendingInvitation,
  pro,
  roleChanged,
  shortenedTo90,
  type Team,
  termsInForce,
  ufukTeam,
} from "./api";
import { mockIdentity } from "./identity";
import { e2eUserEmail } from "./sign-in";

/** The bell's tabs; a page may have tabs of its own. */
const bellTabs = (page: Page) =>
  page.getByRole("tablist", { name: "Which notifications" });

// Screenshots for a pull request. They run only with E2E_SCREENSHOTS=1 and write to apps/web/test-results/screenshots,
// which Git ignores and Playwright empties at the start of each run. Attach them to the pull request; never commit them.
test.skip(
  !process.env.E2E_SCREENSHOTS,
  "Set E2E_SCREENSHOTS=1 to take the screenshots.",
);

// A route that reads Clerk's answer (e2e/identity.ts, and the second factor of the sign-in frames) can still wait for it when
// the test ends. The context then closes under it, the read fails, and Playwright fails the finished test with that error. So
// each test removes its routes before the context closes, and a route still running ends without an error. Waiting for those
// routes instead would hang: a loading shot and the bell's stream hold their answers on purpose.
test.afterEach(({ page }) => page.unrouteAll({ behavior: "ignoreErrors" }));

const folder = "test-results/screenshots";

/**
 * A screen with the Auth footer waits for its "Terms", which comes from a read of its own after the page: Language, Terms and
 * Privacy. A screen without the footer goes on at once.
 */
const footerSettled = async (page: Page) => {
  const footer = page.getByRole("navigation", {
    name: /^(Language and legal|Dil ve yasal bilgiler|اللغة والمعلومات القانونية)$/,
  });
  if ((await footer.count()) === 0) return;
  await expect(footer.getByRole("link")).toHaveCount(2);
};
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

  await page.goto("/settings/profile");
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

// The bell and its list (the Notifications boards): the popover on a desktop and the full screen on a phone in Arabic.
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

/** The Name field of the Profile form. */
const nameField = (page: Page) => page.getByRole("textbox", { name: "Name" });

/** Adds a word to the name, so Save changes turns on (5.02, 8.02). */
const editName = async (page: Page) => {
  await nameField(page).press("End");
  await nameField(page).pressSequentially(" Demir");
};

/** Saves the name; the mocked RenameMe answers, and the toast says so (5.04, 8.04). */
const saveName = async (page: Page) => {
  await editName(page);
  await page.getByRole("button", { name: "Save changes" }).click();
  await page.getByText("Profile saved").waitFor();
};

/** Saves an empty name, so the field says to enter one (5.03, 8.03). */
const emptyName = async (page: Page) => {
  await nameField(page).fill("");
  await page.getByRole("button", { name: "Save changes" }).click();
  await page.getByText("Enter your name.").waitFor();
};

/** Scrolls the theme's card, the one radio group of the Profile page, into view. */
const showTheme = (page: Page) =>
  page.getByRole("radiogroup").scrollIntoViewIfNeeded();

/** The state of the frames that show Google connected and two other sessions (e2e/identity.ts). */
const fullState = (page: Page) =>
  mockIdentity(page, {
    googleEmail: "ayse.demir@gmail.example",
    otherSessions: true,
  });
/** The frame's state with an authenticator that Set up can turn on (6.25 to 6.27). */
const settableState = (page: Page) =>
  mockIdentity(page, {
    googleEmail: "ayse.demir@gmail.example",
    otherSessions: true,
    authenticator: true,
  });

/** Opens Set up; with `steps`, types a code and turns it on (6.26), then saves the codes (6.27). */
const setUp =
  (steps: 0 | 1 | 2 = 0) =>
  async (page: Page) => {
    await otherSessionsShown(page);
    await page.getByRole("button", { name: "Set up" }).click();
    const dialog = page.getByRole("alertdialog", {
      name: "Set up two-step verification",
    });
    // The key sits in a read-only field, whose value the text queries do not see.
    await expect(
      dialog.getByRole("textbox", { name: "Can’t scan it? Enter this key:" }),
    ).toHaveValue(/JBSW/);
    if (steps < 1) return;
    await dialog
      .getByRole("textbox", { name: "Enter the 6-digit code the app shows." })
      .pressSequentially("123456");
    await dialog.getByRole("button", { name: "Turn on" }).click();
    await page
      .getByRole("alertdialog", { name: "Save your backup codes" })
      .waitFor();
    if (steps < 2) return;
    await page.getByRole("button", { name: "I saved them" }).click();
    await page.getByText("Two-step verification is on").waitFor();
  };

/** Waits until the sessions show the last of the other devices. */
const otherSessionsShown = (page: Page) =>
  page.getByText("Firefox on Windows", { exact: true }).waitFor();

/** Waits until Account and security has read the sessions from Clerk: the current one carries "This device". */
const sessionsShown = (page: Page) =>
  page.getByText("This device", { exact: true }).waitFor();

/** Opens Change email; with an address, sends it, and the mocked API answers that the code works for 10 minutes (6.08, 6.09). */
const changeEmail = (address?: string) => async (page: Page) => {
  await otherSessionsShown(page);
  await page.route("**/api/v1/me/email-change", (route) =>
    route.fulfill({
      status: 201,
      json: { expiresAt: new Date(Date.now() + 600_000).toISOString() },
    }),
  );
  await page.getByRole("button", { name: "Change email" }).click();
  if (!address) return;
  await page.getByRole("textbox", { name: "New email" }).fill(address);
  await page.getByRole("button", { name: "Send code" }).click();
  await page.getByRole("alertdialog", { name: "Check your email" }).waitFor();
};

/** A ready export of the person, downloadable for a week (6.21). */
const readyExport = {
  id: "0198f0a2-0000-7000-8000-0000000000e1",
  status: DataExportResponseStatus.Ready,
  requestedAt: "2026-11-05T09:00:00Z",
  readyAt: "2026-11-05T09:04:00Z",
  expiresAt: "2026-11-12T09:04:00Z",
  sizeBytes: 48_213,
};

/** Scrolls to a card of Account and security once the sessions show, so the frame's card is in view. */
const cardShown = (name: string) => async (page: Page) => {
  await otherSessionsShown(page);
  await page.getByRole("region", { name }).scrollIntoViewIfNeeded();
};

/** Opens Export my data; with `request`, asks for it, and the mocked API answers that the worker prepares it (6.19, 6.20). */
const exportData =
  (request = false) =>
  async (page: Page) => {
    await cardShown("Your data")(page);
    await page.route("**/api/v1/me/data-exports", (route) =>
      route.fulfill({
        status: 201,
        json: {
          ...readyExport,
          status: DataExportResponseStatus.Preparing,
          readyAt: null,
          expiresAt: null,
          sizeBytes: null,
        },
      }),
    );
    await page.getByRole("button", { name: "Export my data" }).click();
    if (!request) return;
    await page.getByRole("button", { name: "Request export" }).click();
    await page.getByText("Export started").waitFor();
  };

/** Asks to delete the account; the mocked API refuses the last Owner (6.22) or sends the code (6.23). */
const deleteAccount = (lastOwner: boolean) => async (page: Page) => {
  await cardShown("Delete my account")(page);
  await page.route("**/api/v1/me/account-deletion", (route) =>
    lastOwner
      ? route.fulfill({
          status: 409,
          json: {
            code: ErrorCode.UserLastOwner,
            traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            tenantIds: [northwind.id],
          },
        })
      : route.fulfill({
          status: 201,
          json: { expiresAt: new Date(Date.now() + 600_000).toISOString() },
        }),
  );
  await page.getByRole("button", { name: "Delete my account" }).click();
  await page.getByRole("alertdialog").waitFor();
};

// The General and Members frames of step 37a-frontend-2 (7.01 to 8.45), with the people, counts and dates that the frames draw
// (northwindTeam and ufukTeam of e2e/api.ts).
const general = `${settings}/general`;
const invitationsTab = `${members}?tab=invitations`;
const tenantRead = `**/api/v1/tenants/${northwind.id}`;
const membersRead = `**/api/v1/tenants/${northwind.id}/members?*`;
const invitationsRead = `**/api/v1/tenants/${northwind.id}/invitations?*`;
// The API's answer to a fault: its code and the request's trace, so a screen shows the message and the reference (7.03, 8.03).
const serverError = {
  status: 500,
  json: {
    status: 500,
    title: "Internal Server Error",
    code: ErrorCode.ServerError,
    traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  },
};

/** A plan with a members limit: the seats it gives, and the seats in use, members and pending invitations (F56). */
const seats = (limit: number, used: number): Plan => ({
  ...pro,
  entitlements: { ...pro.entitlements, "members.max": limit },
  usage: { ...pro.usage, "members.max": used },
});

/** Reduced motion stops a skeleton's pulse, so a loading frame is the same in every run. */
const still = (page: Page) => page.emulateMedia({ reducedMotion: "reduce" });

/** The Members tab's list answers so. The tabs' and the settings menu's counts read one item a page, and still come. */
const membersListAnswers =
  (answer: (route: Route) => Promise<void> | void) => (page: Page) =>
    page.route(membersRead, (route) =>
      new URL(route.request().url()).searchParams.get("pageSize") === "1"
        ? route.fallback()
        : answer(route),
    );

/** 500 members, of which the first page is the frame's nine rows; the second page never comes, so "Loading more" stays (8.05). */
const fiveHundredMembers = async (page: Page) => {
  await still(page);
  await page.route(membersRead, (route) =>
    new URL(route.request().url()).searchParams.get("page") === "2"
      ? undefined
      : route.fulfill({
          json: {
            items: manyMembers,
            page: 1,
            pageSize: manyMembers.length,
            total: 500,
          },
        }),
  );
  await page.route(invitationsRead, (route) =>
    route.fulfill({ json: { items: [], page: 1, pageSize: 1, total: 48 } }),
  );
};

/** Opens the ⋯ menu of a row: in the table, or in the person's card on a phone, where the table is hidden. */
const openRowMenu = (name: string) => (page: Page) =>
  page
    .getByRole("button", { name: `Actions for ${name}` })
    .filter({ visible: true })
    .click();

/** Opens a row's menu, picks the item and waits for its dialog. */
const rowDialog = (name: string, item: string) => async (page: Page) => {
  await openRowMenu(name)(page);
  await page.getByRole("menuitem", { name: item }).click();
  await page.getByRole("alertdialog").waitFor();
};

/** Opens the menu beside the title (8.41). */
const openMoreMenu = (page: Page) =>
  page.getByRole("button", { name: "More actions" }).click();

/** Opens Export members from the menu beside the title (8.08). */
const openExport = async (page: Page) => {
  await openMoreMenu(page);
  await page.getByRole("menuitem", { name: "Export CSV" }).click();
  await page.getByRole("alertdialog").waitFor();
};

/** Exports the file; the mocked route answers it, or a problem (8.12, 8.16). */
const exportFile = (done: string) => async (page: Page) => {
  await openExport(page);
  await page
    .getByRole("alertdialog")
    .getByRole("button", { name: "Export CSV" })
    .click();
  // A toast's text may show twice: on screen and in its live region.
  await page.getByText(done).first().waitFor();
};

/** Opens the invite dialog, or "No free seat" at and over the limit, with the button of the page's language. */
const openInvite =
  (label = "Invite member") =>
  async (page: Page) => {
    await page.getByRole("button", { name: label }).first().click();
    await page.getByRole("alertdialog").waitFor();
  };

/** The invite dialog's Email field, its first text field in every language. */
const inviteEmail = (page: Page) =>
  page.getByRole("alertdialog").getByRole("textbox").first();

/** Opens the invite dialog with the Email field in focus, as the frames draw it (8.26, 8.35, 8.37). */
const inviteFocused =
  (label = "Invite member") =>
  async (page: Page) => {
    await openInvite(label)(page);
    await inviteEmail(page).focus();
  };

/** Sends the invitation to the address and waits for what the dialog or the page then says (8.27, 8.28, 8.30). */
const sendInvitation = (email: string, says: string) => async (page: Page) => {
  await openInvite()(page);
  await inviteEmail(page).fill(email);
  await page
    .getByRole("alertdialog")
    .getByRole("button", { name: "Send invitation" })
    .click();
  await page.getByText(says, { exact: true }).first().waitFor();
};

/** The person is in no organisation: ListMyTenants answers none, and GetMe names the frame's Lena Fischer (5.11, 6.33, 2.45). */
const withoutAnOrganisation =
  (language: Language = Language.En) =>
  async (page: Page) => {
    await memberOf([])(page);
    await lena(language)(page);
  };

/** Lena's identity in the Account and security frames without an organisation: Google connected, two other sessions (6.33). */
const lenaIdentity = (page: Page) =>
  mockIdentity(page, {
    googleEmail: "lena.fischer@gmail.example",
    otherSessions: true,
  });

/** ListMembers filtered by the Owner role answers `count` Owners: the read that tells whether the viewer is the last Owner (8.46). */
const ownersAre = (count: number) => (page: Page) =>
  page.route(`**/api/v1/tenants/${northwind.id}/members?*`, (route) =>
    new URL(route.request().url()).searchParams.get("roleId") ===
    builtInRoles.owner.id
      ? route.fulfill({
          json: { items: [], page: 1, pageSize: 1, total: count },
        })
      : route.fallback(),
  );

/** LeaveTenant answers success, and ListMyTenants answers none from then on (8.24); or the given problem (8.25). */
const leaveAnswers =
  (refusal?: { status: number; code: ErrorCode }) => async (page: Page) => {
    let left = false;
    await page.route(
      `**/api/v1/tenants/${northwind.id}/membership`,
      (route) => {
        if (refusal)
          return route.fulfill({
            status: refusal.status,
            json: { ...refusal, title: "Refused", traceId: "00-e2e" },
          });
        left = true;
        return route.fulfill({ status: 204 });
      },
    );
    await page.route("**/api/v1/me/tenants*", (route) =>
      left
        ? route.fulfill({
            json: { items: [], page: 1, pageSize: 100, total: 0 },
          })
        : route.fallback(),
    );
  };

const shellShots: {
  file: string;
  size: keyof typeof frames;
  language?: Language;
  scheme?: "light" | "dark";
  role?: BuiltInRole;
  path?: string;
  /** The organisation's plan, Pro by default. */
  plan?: Plan;
  /** The frame's people, in place of the boards' (e2e/api.ts). */
  team?: Team;
  mock?: (page: Page) => Promise<unknown>;
  /** What shows when the page has loaded, for a frame of a page that loads or fails: the page's heading and no busy list by default. */
  ready?: (page: Page) => Promise<unknown>;
  open?: (page: Page) => Promise<void>;
  /** The frame draws the pointer over something, so `open` leaves it there; else the pointer moves away after `open`. */
  hovers?: true;
  /** The whole page, for a frame taller than the screen. */
  fullPage?: boolean;
}[] = [
  { file: "2.01-home-en-light-desktop", size: "desktop" },
  {
    file: "4.02-home-member-en-light-desktop",
    size: "desktop",
    role: BuiltInRole.Member,
  },
  {
    file: "4.03-home-terms-notice-en-light-desktop",
    size: "desktop",
    mock: (page) =>
      mockMyTerms(page, {
        action: MyTermsResponseAction.Notice,
        version: { ...decemberTerms, requiresAcceptance: false },
      }),
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
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "5.08-profile-en-light-tablet",
    size: "tablet",
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "5.09-profile-en-light-phone",
    size: "phone",
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "5.07-profile-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/settings/profile",
    open: async (page) => {
      await page.getByRole("radio", { name: "Dark" }).click();
      await showTheme(page);
    },
  },
  {
    file: "5.10-profile-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "5.02-profile-edited-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    open: editName,
  },
  {
    file: "5.03-profile-name-empty-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    open: emptyName,
  },
  {
    file: "5.04-profile-saved-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    mock: (page) => mockRename(page, Language.En),
    open: saveName,
  },
  // The pages without an organisation (5.11 to 5.14, 6.33 to 6.35, 2.45): Lena Fischer is in no organisation.
  {
    file: "5.11-profile-no-organisation-en-light-desktop",
    size: "desktop",
    fullPage: true,
    path: "/settings/profile",
    mock: withoutAnOrganisation(),
  },
  {
    file: "5.12-profile-no-organisation-en-light-phone",
    size: "phone",
    path: "/settings/profile",
    mock: withoutAnOrganisation(),
  },
  {
    file: "5.13-profile-no-organisation-en-dark-desktop",
    size: "desktop",
    fullPage: true,
    scheme: "dark",
    path: "/settings/profile",
    mock: withoutAnOrganisation(),
    open: (page) => page.getByRole("radio", { name: "Dark" }).click(),
  },
  {
    file: "5.14-profile-no-organisation-ar-light-desktop",
    size: "desktop",
    fullPage: true,
    language: Language.Ar,
    path: "/settings/profile",
    mock: withoutAnOrganisation(Language.Ar),
  },
  {
    file: "6.33-account-no-organisation-en-light-desktop",
    size: "desktop",
    fullPage: true,
    path: "/settings/account",
    mock: async (page) => {
      await withoutAnOrganisation()(page);
      await lenaIdentity(page);
    },
    open: otherSessionsShown,
  },
  {
    file: "6.34-account-no-organisation-en-light-phone",
    size: "phone",
    fullPage: true,
    path: "/settings/account",
    mock: async (page) => {
      await withoutAnOrganisation()(page);
      await lenaIdentity(page);
    },
    open: otherSessionsShown,
  },
  {
    file: "6.35-account-no-organisation-en-dark-desktop",
    size: "desktop",
    fullPage: true,
    scheme: "dark",
    path: "/settings/account",
    mock: async (page) => {
      await withoutAnOrganisation()(page);
      await lenaIdentity(page);
    },
    open: otherSessionsShown,
  },
  {
    file: "2.45-settings-drawer-no-organisation-en-light-phone",
    size: "phone",
    path: "/settings/profile",
    mock: withoutAnOrganisation(),
    open: (page) => page.getByRole("button", { name: "Open menu" }).click(),
  },
  // The Dates and times pattern: a time's line on hover, the city and its offset (F18, F60).
  {
    file: "pattern-time-en-light-desktop",
    size: "desktop",
    hovers: true,
    path: `${settings}/activity`,
    mock: viewer(Language.En, BuiltInRole.Owner),
    open: async (page) => {
      await page.getByRole("main").locator("time").first().hover();
      // The tooltip's line ends with the zone's city and offset; the shot waits for that text, as time.test.tsx does.
      await page.getByText(/ · .+ \(UTC[+-]\d+(:\d+)?\)$/).waitFor();
    },
  },
  // Account and security (F62): 6.01, 6.06 and 6.07 show Google connected and two other sessions; 6.05 this device alone with no
  // linked account. The shared test user gathers a session in every run, so Clerk's answers are changed on the way to the page.
  {
    file: "6.01-account-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: otherSessionsShown,
  },
  {
    file: "6.05-account-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: (page) => mockIdentity(page, {}),
    open: sessionsShown,
  },
  {
    file: "6.06-account-en-dark-desktop",
    fullPage: true,
    size: "desktop",
    scheme: "dark",
    path: "/settings/account",
    mock: fullState,
    open: otherSessionsShown,
  },
  {
    file: "6.07-account-en-light-phone",
    fullPage: true,
    size: "phone",
    path: "/settings/account",
    mock: fullState,
    open: otherSessionsShown,
  },
  {
    file: "6.08-change-email-en-light-desktop",
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: changeEmail(),
  },
  {
    file: "6.09-email-code-en-light-desktop",
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: changeEmail("ayse.demir@northwind-traders.example"),
  },
  // 6.25 to 6.27: Set up, the backup codes and the authenticator on; Clerk's answers of Set up are the frame's (e2e/identity.ts).
  {
    file: "6.25-two-step-set-up-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: settableState,
    open: setUp(),
  },
  {
    file: "6.26-backup-codes-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: settableState,
    open: setUp(1),
  },
  {
    file: "6.27-two-step-on-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: settableState,
    open: setUp(2),
  },
  {
    file: "6.19-export-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: exportData(),
  },
  {
    file: "6.20-export-requested-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: exportData(true),
  },
  {
    file: "6.21-export-ready-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: async (page) => {
      await fullState(page);
      await mockDataExport(page, readyExport);
    },
    open: cardShown("Your data"),
  },
  {
    file: "6.22-delete-refused-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: deleteAccount(true),
  },
  {
    file: "6.23-delete-code-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: deleteAccount(false),
  },
  // The page after the deletion reads the address from the tab's storage, which Delete my account writes (F62).
  {
    file: "6.24-account-deleted-en-light-desktop",
    size: "desktop",
    path: "/account/deleted",
    mock: (page) =>
      page.addInitScript(() =>
        sessionStorage.setItem(
          "deleted-account-email",
          "ayse.demir@northwind.example",
        ),
      ),
  },
  // General (7.01 to 7.05). 7.02 is the shell's own skeleton while GetMyTenant comes (the maintainer's decision of 2026-11-07); 7.03 its problem in the content.
  {
    file: "7.01-general-en-light-desktop",
    size: "desktop",
    path: general,
    team: northwindTeam(),
  },
  {
    file: "7.02-general-loading-en-light-desktop",
    size: "desktop",
    path: general,
    team: northwindTeam(),
    // GetMyTenant never answers.
    mock: async (page) => {
      await still(page);
      await page.route(tenantRead, () => {});
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading", exact: true }).waitFor(),
  },
  {
    file: "7.03-general-error-en-light-desktop",
    size: "desktop",
    path: general,
    team: northwindTeam(),
    // GetMyTenant fails; the query tries three times more before the page says so (F8).
    mock: (page) =>
      page.route(tenantRead, (route) => route.fulfill(serverError)),
    ready: (page) =>
      page.getByRole("heading", { name: "Something went wrong" }).waitFor(),
  },
  {
    file: "7.04-general-member-en-light-desktop",
    size: "desktop",
    role: BuiltInRole.Member,
    path: general,
    team: northwindTeam(),
  },
  {
    file: "7.05-general-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: general,
    team: northwindTeam(),
  },
  // Members (8.01 to 8.45).
  {
    file: "8.01-members-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
  },
  {
    file: "8.02-members-loading-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    // The list never answers.
    mock: async (page) => {
      await still(page);
      await membersListAnswers(() => {})(page);
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading members" }).waitFor(),
  },
  {
    file: "8.03-members-error-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    // The list fails; the query tries three times more before the page says so (F8).
    mock: membersListAnswers((route) => route.fulfill(serverError)),
    ready: (page) =>
      page.getByRole("heading", { name: "Couldn’t load members" }).waitFor(),
  },
  {
    file: "8.04-members-only-you-en-light-desktop",
    size: "desktop",
    path: members,
    team: { ...northwindTeam(1), invitations: [] },
  },
  {
    file: "8.05-members-loading-more-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    mock: fiveHundredMembers,
    open: async (page) => {
      await page.getByRole("button", { name: "Load more" }).click();
      await page.getByText("Loading more members").waitFor();
    },
  },
  {
    file: "8.06-members-no-match-en-light-desktop",
    size: "desktop",
    path: `${members}?q=zzq`,
    team: northwindTeam(),
    open: (page) =>
      page.getByRole("searchbox", { name: "Search by name or email" }).focus(),
  },
  {
    file: "8.07-filter-by-role-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    open: async (page) => {
      await page
        .getByRole("button", { name: "Filter by role: All roles" })
        .click();
      await page.getByRole("menuitemradio", { name: "All roles" }).waitFor();
    },
  },
  {
    file: "8.08-export-members-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    open: openExport,
  },
  {
    file: "8.09-members-over-limit-en-light-desktop",
    size: "desktop",
    path: members,
    plan: seats(15, 17),
    team: northwindTeam(15),
  },
  {
    file: "8.10-export-members-filtered-en-light-desktop",
    size: "desktop",
    path: `${members}?roleId=${builtInRoles.admin.id}`,
    team: northwindTeam(),
    open: openExport,
  },
  {
    file: "8.11-members-member-en-light-desktop",
    size: "desktop",
    role: BuiltInRole.Member,
    path: members,
    team: northwindTeam(),
  },
  {
    file: "8.12-export-members-downloaded-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    mock: (page) => mockMembersExport(page, "file"),
    open: exportFile("Members exported"),
  },
  {
    file: "8.13-members-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: members,
    team: northwindTeam(),
  },
  {
    file: "8.14-export-nothing-en-light-desktop",
    size: "desktop",
    path: `${members}?q=zzq`,
    team: northwindTeam(),
    open: async (page) => {
      await openMoreMenu(page);
      await page.getByText("No member matches the search.").waitFor();
    },
  },
  {
    file: "8.15-members-focus-hover-en-light-desktop",
    size: "desktop",
    hovers: true,
    path: members,
    team: northwindTeam(),
    // The keyboard's focus on Lena Fischer's ⋯ (a key press, so :focus-visible draws the ring), the pointer over Omar Haddad's row.
    open: async (page) => {
      await page
        .getByRole("button", { name: "Actions for Lena Fischer" })
        .focus();
      await page.keyboard.press("Shift+Tab");
      await page.keyboard.press("Tab");
      await page.getByRole("row", { name: /Omar Haddad/ }).hover();
    },
  },
  {
    file: "8.16-export-members-error-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    mock: (page) => mockMembersExport(page, "problem"),
    open: exportFile("Couldn’t create the file."),
  },
  {
    file: "8.17-row-actions-en-light-desktop",
    size: "desktop",
    hovers: true,
    path: members,
    team: northwindTeam(),
    open: async (page) => {
      await openRowMenu("Omar Haddad")(page);
      await page.getByRole("menuitem", { name: "Change role" }).hover();
    },
  },
  // 8.18: the own row offers Change role and Leave. The frame's team has one Owner, so the shot answers two, as the frame draws
  // Leave enabled; 8.46 draws the one Owner.
  {
    file: "8.18-own-row-actions-en-light-desktop",
    size: "desktop",
    hovers: true,
    path: members,
    team: northwindTeam(),
    mock: ownersAre(2),
    open: async (page) => {
      await openRowMenu("Ayşe Demir")(page);
      await page.getByRole("menuitem", { name: "Leave organisation" }).hover();
    },
  },
  {
    file: "8.46-own-row-actions-last-owner-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    open: async (page) => {
      await openRowMenu("Ayşe Demir")(page);
      await page
        .getByText("You are the last Owner.", { exact: false })
        .waitFor();
    },
  },
  {
    file: "8.23-leave-organisation-en-light-desktop",
    size: "desktop",
    role: BuiltInRole.Member,
    path: members,
    team: northwindTeam(),
    open: rowDialog("Lena Fischer", "Leave organisation"),
  },
  {
    file: "8.24-left-no-access-yet-en-light-desktop",
    size: "desktop",
    role: BuiltInRole.Member,
    path: members,
    team: northwindTeam(),
    mock: leaveAnswers(),
    open: async (page) => {
      await rowDialog("Lena Fischer", "Leave organisation")(page);
      await page
        .getByRole("alertdialog")
        .getByRole("button", { name: "Leave organisation" })
        .click();
      await page.getByRole("heading", { name: "No access yet" }).waitFor();
      await page.getByText("You left Northwind Traders").first().waitFor();
    },
  },
  {
    file: "8.25-leave-last-owner-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    mock: async (page) => {
      await ownersAre(2)(page);
      await leaveAnswers({ status: 409, code: ErrorCode.MemberLastOwner })(
        page,
      );
    },
    open: async (page) => {
      await rowDialog("Ayşe Demir", "Leave organisation")(page);
      await page
        .getByRole("alertdialog")
        .getByRole("button", { name: "Leave organisation" })
        .click();
      await page.getByRole("button", { name: "Close" }).first().waitFor();
    },
  },
  {
    file: "8.19-change-role-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    open: rowDialog("Omar Haddad", "Change role"),
  },
  {
    file: "8.20-change-role-last-owner-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    open: rowDialog("Ayşe Demir", "Change role"),
  },
  {
    file: "8.21-remove-member-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    open: rowDialog("Omar Haddad", "Remove from organisation"),
  },
  {
    file: "8.22-member-removed-en-light-desktop",
    size: "desktop",
    path: members,
    team: northwindTeam(),
    open: async (page) => {
      await rowDialog("Omar Haddad", "Remove from organisation")(page);
      await page.getByRole("button", { name: "Remove member" }).click();
      await page
        .getByText("Omar Haddad removed from Northwind Traders")
        .first()
        .waitFor();
      await expect(page.getByRole("row", { name: /Omar Haddad/ })).toHaveCount(
        0,
      );
    },
  },
  {
    file: "8.26-invite-member-en-light-desktop",
    size: "desktop",
    path: members,
    plan: seats(16, 13),
    team: northwindTeam(),
    open: inviteFocused(),
  },
  {
    file: "8.27-invite-invalid-email-en-light-desktop",
    size: "desktop",
    path: members,
    plan: seats(16, 13),
    team: northwindTeam(),
    open: sendInvitation(
      "lena@northwind",
      "Enter an email address like name@company.com.",
    ),
  },
  {
    file: "8.28-invite-already-member-en-light-desktop",
    size: "desktop",
    path: members,
    plan: seats(16, 13),
    team: northwindTeam(),
    open: sendInvitation("lena@northwind.example", "Invitation not sent"),
  },
  {
    file: "8.29-invitations-en-light-desktop",
    size: "desktop",
    path: invitationsTab,
    team: northwindTeam(),
  },
  {
    file: "8.30-invitation-sent-en-light-desktop",
    size: "desktop",
    path: invitationsTab,
    team: northwindTeam(),
    open: async (page) => {
      await sendInvitation(
        "maximiliane.von-hohenzollern@internationale-handelsgesellschaft.example",
        "Invitation sent",
      )(page);
      await page.getByRole("row", { name: /maximiliane/ }).waitFor();
    },
  },
  {
    file: "8.31-invitations-empty-en-light-desktop",
    size: "desktop",
    path: invitationsTab,
    team: { ...northwindTeam(), invitations: [] },
  },
  {
    file: "8.32-revoke-invitation-en-light-desktop",
    size: "desktop",
    path: invitationsTab,
    team: northwindTeam(),
    open: rowDialog("david.okafor@harbour.example", "Revoke invitation"),
  },
  {
    file: "8.33-members-en-light-tablet",
    size: "tablet",
    path: members,
    team: northwindTeam(),
  },
  {
    file: "8.34-members-en-light-phone",
    size: "phone",
    path: members,
    team: northwindTeam(),
  },
  {
    file: "8.35-invite-member-en-light-phone",
    size: "phone",
    path: members,
    plan: seats(16, 13),
    team: northwindTeam(),
    open: inviteFocused(),
  },
  {
    file: "8.36-members-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: members,
    team: ufukTeam,
  },
  {
    file: "8.36-remove-member-en-light-phone",
    size: "phone",
    path: members,
    team: northwindTeam(),
    open: rowDialog("Omar Haddad", "Remove from organisation"),
  },
  {
    file: "8.37-invite-member-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: members,
    plan: seats(15, 12),
    team: ufukTeam,
    open: inviteFocused("دعوة عضو"),
  },
  {
    file: "8.38-change-role-admin-en-light-desktop",
    size: "desktop",
    role: BuiltInRole.Admin,
    path: members,
    team: northwindTeam(),
    // The frame draws the locked Auditor card at the list's end, and the page behind at its top.
    open: async (page) => {
      await rowDialog("Lena Fischer", "Change role")(page);
      await page
        .getByRole("alertdialog")
        .locator("label", {
          has: page.getByRole("radio", { name: "Auditor" }),
        })
        .scrollIntoViewIfNeeded();
      // The scroll lock may move the page's scroll onto <body>, so both go back to the top.
      await page.evaluate(() => {
        window.scrollTo(0, 0);
        document.body.scrollTop = 0;
      });
    },
  },
  {
    file: "8.41-more-menu-en-light-desktop",
    size: "desktop",
    hovers: true,
    path: members,
    team: northwindTeam(),
    open: async (page) => {
      await openMoreMenu(page);
      await page.getByRole("menuitem", { name: "Export CSV" }).hover();
    },
  },
  {
    file: "8.43-no-free-seat-en-light-desktop",
    size: "desktop",
    path: members,
    plan: seats(15, 15),
    team: northwindTeam(13),
    open: openInvite(),
  },
  {
    file: "8.45-no-free-seat-over-en-light-desktop",
    size: "desktop",
    path: members,
    plan: seats(15, 17),
    team: northwindTeam(15),
    open: openInvite(),
  },
];

// Roles (9.01 to 9.20) and the Activity log (12.01 to 12.08), with the people of the Members frames (northwindTeam).
const rolesRead = `**/api/v1/tenants/${northwind.id}/roles?*`;
const auditLogRead = `**/api/v1/tenants/${northwind.id}/audit-log*`;
const rolePage = (name: string) =>
  `${roles}/${northwindTeam().customRoles.find((role) => role.name === name)?.id}`;

/** The Roles list's pages answer so (50 a page). The settings menu's count reads the one page of 100 (F51), and still comes. */
const rolesListAnswers =
  (answer: (route: Route) => Promise<void> | void) => (page: Page) =>
    page.route(rolesRead, (route) =>
      new URL(route.request().url()).searchParams.get("pageSize") === "50"
        ? answer(route)
        : route.fallback(),
    );

/** A team whose custom roles CreateRole and UpdateRole change as the API does, so the list and the role read them back. */
const editableTeam = () => {
  const team = northwindTeam();
  const mock = (page: Page) =>
    page.route(`**/api/v1/tenants/${northwind.id}/roles**`, async (route) => {
      const request = route.request();
      if (request.method() === "GET") return route.fallback();
      const body = request.postDataJSON() as {
        name: string;
        permissions: Permission[];
      };
      const id = new URL(request.url()).pathname.split("/").pop();
      const old = team.customRoles.find((role) => role.id === id);
      const saved: RoleDetailResponse = {
        id: old?.id ?? "0198f0a2-0000-7000-8000-0000000002c9",
        builtIn: false,
        memberCount: 0,
        pendingInvitationCount: 0,
        ...body,
      };
      team.customRoles = old
        ? team.customRoles.map((role) => (role === old ? saved : role))
        : [...team.customRoles, saved];
      return route.fulfill({ status: old ? 200 : 201, json: saved });
    });
  return { team, mock };
};

/** Types into the role's name field of the editor. */
const roleName = (page: Page) =>
  page.getByRole("main").getByRole("textbox", { name: "Name" });

/** Opens the More menu of the log and Export CSV, and waits for its dialog (12.08). */
const openLogExport = async (page: Page) => {
  await page.getByRole("button", { name: "More actions" }).click();
  await page.getByRole("menuitem", { name: "Export CSV" }).click();
  await page.getByRole("alertdialog").waitFor();
};

const created = editableTeam();
const saved = editableTeam();

shellShots.push(
  {
    file: "9.01-roles-en-light-desktop",
    size: "desktop",
    path: roles,
    team: northwindTeam(),
  },
  {
    file: "9.02-roles-loading-en-light-desktop",
    size: "desktop",
    path: roles,
    team: northwindTeam(),
    // The list never answers.
    mock: async (page) => {
      await still(page);
      await rolesListAnswers(() => {})(page);
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading roles" }).waitFor(),
  },
  {
    file: "9.03-roles-error-en-light-desktop",
    size: "desktop",
    path: roles,
    team: northwindTeam(),
    // The list fails; the query tries three times more before the page says so (F8).
    mock: rolesListAnswers((route) => route.fulfill(serverError)),
    ready: (page) =>
      page.getByRole("heading", { name: "Couldn’t load roles" }).waitFor(),
  },
  {
    file: "9.04-roles-no-custom-en-light-desktop",
    size: "desktop",
    path: roles,
    team: { ...northwindTeam(), customRoles: [] },
  },
  {
    file: "9.05-role-created-en-light-desktop",
    size: "desktop",
    path: `${roles}/new`,
    team: created.team,
    mock: created.mock,
    open: async (page) => {
      await roleName(page).fill("Maintenance planner");
      await page.getByRole("button", { name: "Create role" }).click();
      // The list first: the toast shows before the navigation ends, and the editor's animations end only when it goes.
      await page.getByRole("heading", { level: 1, name: "Roles" }).waitFor();
      await page
        .getByText("Maintenance planner is ready to give.")
        .first()
        .waitFor();
    },
  },
  {
    file: "9.06-roles-plan-off-en-light-desktop",
    size: "desktop",
    path: roles,
    plan: customRolesOff,
    team: northwindTeam(),
  },
  {
    // No frame draws Custom roles on with a limit of 0. The nearest is 9.06: the plan's reason, with nothing to delete.
    file: "9.06-roles-limit-zero-en-light-desktop",
    size: "desktop",
    path: roles,
    plan: planWith({ "roles.custom.max": 0 }, { "roles.custom.max": 0 }),
    team: { ...northwindTeam(), customRoles: [] },
  },
  {
    file: "9.07-roles-over-the-limit-en-light-desktop",
    size: "desktop",
    path: roles,
    plan: planWith({ "roles.custom.max": 10 }, { "roles.custom.max": 12 }),
    team: northwindTeam(),
  },
  {
    file: "9.08-new-role-en-light-desktop",
    size: "desktop",
    path: `${roles}/new`,
    team: northwindTeam(),
    open: async (page) => {
      await roleName(page).fill("Maintenance planner");
      await page.getByRole("checkbox", { name: "See members" }).click();
    },
  },
  {
    file: "9.09-new-role-name-taken-en-light-desktop",
    size: "desktop",
    path: `${roles}/new`,
    team: northwindTeam(),
    open: async (page) => {
      await roleName(page).fill("Admin");
      await page.getByRole("button", { name: "Create role" }).click();
      await page.getByText("A role named Admin already exists.").waitFor();
    },
  },
  {
    file: "9.10-role-built-in-en-light-desktop",
    size: "desktop",
    path: `${roles}/${builtInRoles.admin.id}`,
    team: northwindTeam(),
  },
  {
    file: "9.11-role-edit-en-light-desktop",
    size: "desktop",
    path: rolePage("Support lead"),
    team: northwindTeam(),
  },
  {
    file: "9.12-delete-role-en-light-desktop",
    size: "desktop",
    path: roles,
    team: northwindTeam(),
    open: rowDialog("Auditor", "Delete role"),
  },
  {
    file: "9.13-delete-role-in-use-en-light-desktop",
    size: "desktop",
    path: roles,
    team: northwindTeam(),
    open: rowDialog("Support lead", "Delete role"),
  },
  {
    file: "9.15-discard-changes-en-light-desktop",
    size: "desktop",
    path: rolePage("Support lead"),
    team: northwindTeam(),
    open: async (page) => {
      await page.getByRole("checkbox", { name: "See invitations" }).click();
      await page.getByRole("button", { name: "Back to Roles" }).click();
      await page.getByRole("alertdialog").waitFor();
    },
  },
  {
    file: "9.16-role-saved-en-light-desktop",
    size: "desktop",
    path: rolePage("Support lead"),
    team: saved.team,
    mock: saved.mock,
    open: async (page) => {
      await page.getByRole("checkbox", { name: "See invitations" }).click();
      await page.getByRole("button", { name: "Save changes" }).click();
      await page
        .getByText(/^Support lead now has/)
        .first()
        .waitFor();
      // The frame shows the role from its top, with the save bar at the window's foot.
      await page.evaluate(() => window.scrollTo(0, 0));
    },
  },
  {
    file: "9.17-roles-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: roles,
    team: northwindTeam(),
  },
  {
    file: "9.20-role-out-of-reach-en-light-desktop",
    size: "desktop",
    path: rolePage("Onboarding"),
    team: northwindTeam(),
    // The viewer is Liam, who holds Role manager, which lacks Onboarding's invitations (F51): GetMe answers him and
    // GetMyTenant his role and permissions, as the team's mock answers the rest.
    mock: async (page) => {
      const team = northwindTeam();
      const manager = team.customRoles.find(
        (role) => role.name === "Role manager",
      );
      const liam = team.members.find(
        (member) => member.role.id === manager?.id,
      );
      await page.route("**/api/v1/me", (route) =>
        route.fulfill({
          json: {
            ...me(Language.En),
            id: liam?.userId,
            name: liam?.name,
            email: liam?.email,
          },
        }),
      );
      await page.route(`**/api/v1/tenants/${northwind.id}`, (route) =>
        route.fulfill({
          json: {
            ...northwind,
            role: { id: manager?.id, name: manager?.name, builtIn: false },
            createdAt: northwindTeam().createdAt,
            ...pro,
            permissions: manager?.permissions,
          },
        }),
      );
    },
  },
  {
    file: "12.01-activity-log-en-light-desktop",
    size: "desktop",
    fullPage: true,
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: (page) => mockActivityFrame(page),
  },
  {
    file: "12.02-activity-log-loading-older-en-light-desktop",
    size: "desktop",
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: async (page) => {
      await still(page);
      await mockActivityFrame(page, "hold");
    },
    open: async (page) => {
      await page.getByRole("button", { name: "Show older activity" }).click();
      await page.getByText("Loading older activity…").waitFor();
    },
  },
  {
    file: "12.03-activity-log-loading-en-light-desktop",
    size: "desktop",
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: async (page) => {
      await still(page);
      await page.route(auditLogRead, () => {});
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading activity" }).waitFor(),
  },
  {
    file: "12.04-activity-log-error-en-light-desktop",
    size: "desktop",
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: (page) =>
      page.route(auditLogRead, (route) => route.fulfill(serverError)),
    ready: (page) =>
      page.getByRole("heading", { name: "Couldn’t load activity" }).waitFor(),
  },
  {
    file: "12.05-activity-log-empty-en-light-desktop",
    size: "desktop",
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: (page) =>
      page.route(auditLogRead, (route) =>
        route.fulfill({ json: { items: [], nextCursor: null } }),
      ),
  },
  {
    file: "12.06-activity-log-older-failed-en-light-desktop",
    size: "desktop",
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: (page) => mockActivityFrame(page, "fail"),
    open: async (page) => {
      await page.getByRole("button", { name: "Show older activity" }).click();
      await page.getByText("Couldn’t load older activity.").waitFor();
    },
  },
  {
    file: "12.07-activity-log-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: (page) => mockActivityFrame(page),
  },
  {
    file: "12.08-export-activity-log-en-light-desktop",
    size: "desktop",
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: (page) => mockActivityFrame(page),
    open: openLogExport,
  },
  {
    file: "12.09-export-activity-log-capped-en-light-desktop",
    size: "desktop",
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: async (page) => {
      await mockActivityFrame(page);
      await mockActivityExport(page, activityFileCap);
    },
    open: async (page) => {
      await openLogExport(page);
      await page
        .getByRole("alertdialog")
        .getByRole("button", { name: "Export CSV" })
        .click();
      await page
        .getByText("The file holds the newest 100,000 entries.")
        .waitFor();
    },
  },
  {
    file: "12.10-activity-log-older-entries-go-en-light-desktop",
    size: "desktop",
    fullPage: true,
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: async (page) => {
      await mockActivityFrame(page);
      await mockRetention(page, shortenedTo90);
    },
    ready: (page) =>
      page.getByText("Entries older than 90 days go on 1 Dec 2026.").waitFor(),
  },
  {
    file: "12.11-activity-log-older-entries-go-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    fullPage: true,
    path: `${settings}/activity`,
    team: northwindTeam(),
    mock: async (page) => {
      await mockActivityFrame(page);
      await mockRetention(page, shortenedTo90);
    },
    ready: (page) =>
      page.getByText("Entries older than 90 days go on 1 Dec 2026.").waitFor(),
  },
);

/** The page has loaded: its heading shows, and no list is busy. */
const loaded = async (page: Page) => {
  await page.getByRole("main").getByRole("heading").first().waitFor();
  await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
};

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
    await mockMembers(page, {
      language: shot.language,
      role: shot.role,
      plan: shot.plan,
      team: shot.team,
    });
    await shot.mock?.(page);

    await page.goto(shot.path);
    await (shot.ready ?? loaded)(page);
    await shot.open?.(page);
    // No pointer over a control unless the frame draws one: a click leaves its hover behind (8.12).
    if (!shot.hovers) await page.mouse.move(0, 0);
    // The screen as it rests: every opening animation has finished. A popup mounts a frame or two after the click, so its
    // animation starts only then. An endless animation, such as a skeleton's pulse, has no end to wait for.
    await page.evaluate(
      () =>
        new Promise((done) =>
          requestAnimationFrame(() => requestAnimationFrame(done)),
        ),
    );
    await page.evaluate(() =>
      Promise.all(
        document
          .getAnimations()
          .filter(
            (each) =>
              each.effect?.getTiming().iterations !== Number.POSITIVE_INFINITY,
          )
          .map((each) => each.finished),
      ),
    );
    // A tall frame draws the whole page as one screen, so the sticky sidebar reaches its foot. A full-page capture alone keeps the
    // sidebar at the screen's height and leaves the account chip mid-page (6.33).
    if (shot.fullPage) {
      const height = await page.evaluate(
        () => document.documentElement.scrollHeight,
      );
      await page.setViewportSize({
        width: frames[shot.size].width,
        height: Math.max(height, frames[shot.size].height),
      });
    }

    await page.screenshot({
      path: `${folder}/shell-${shot.file}.png`,
      fullPage: shot.fullPage,
    });
  });

// The pages before an organisation opens (the Entry frames 1.13 to 1.19 and 1.40 to 1.51, and 2.13 and 2.14), signed in, at the
// frames' sizes. The name gives the frame.
const acceptPage = `/invitations/accept?code=${encodeURIComponent(invitationCode)}`;
const harbour = {
  ...northwind,
  id: "0198f0a2-0000-7000-8000-0000000000b3",
  slug: "harbour-logistics",
  name: "Harbour Logistics",
  role: {
    id: builtInRoles.member.id,
    name: builtInRoles.member.name,
    builtIn: builtInRoles.member.builtIn,
  },
};

/** ListMyTenants answers these organisations. */
const memberOf = (items: (typeof northwind)[]) => (page: Page) =>
  page.route("**/api/v1/me/tenants*", (route) =>
    route.fulfill({
      json: { items, page: 1, pageSize: 100, total: items.length },
    }),
  );

/** GetMe refuses the account (A9), on every page. */
const accountRefused = (code: ErrorCode) => (page: Page) =>
  page.route("**/api/v1/me", (route) =>
    route.fulfill({
      status: 403,
      json: { status: 403, title: "Refused", code, traceId: "00-e2e" },
    }),
  );

/** The frame's person: Lena Fischer, with no organisation, in Istanbul, with two unread notifications (1.19, 5.11, 6.33). */
const lena =
  (language: Language = Language.En) =>
  async (page: Page) => {
    await page.route("**/api/v1/me", (route) =>
      route.fulfill({
        json: {
          ...me(language),
          name: "Lena Fischer",
          email: "lena@northwind.example",
          timeZone: "Europe/Istanbul",
        },
      }),
    );
    await page.route("**/api/v1/me/notifications/unread-count", (route) =>
      route.fulfill({ json: { unread: 2 } }),
    );
  };

const omar = {
  ...pendingInvitation,
  inviterName: { ...pendingInvitation.inviterName, name: "Omar Haddad" },
};

/** The gate after sign-in (1.09 to 1.12, 1.14, 1.55, 1.56): GetMyTerms asks for the frames' new version on every page. */
const asksForTerms =
  (version = decemberTerms) =>
  async (page: Page) => {
    await mockApi(page);
    await mockMyTerms(page, {
      action: MyTermsResponseAction.Acceptance,
      version,
    });
  };
const agreeToTerms = (page: Page) =>
  page.getByRole("checkbox", { name: /Terms of use|شروط الاستخدام/ }).click();
const acceptTerms = async (page: Page) => {
  await agreeToTerms(page);
  await page.getByRole("button", { name: "Accept" }).click();
};

const entryShots: {
  file: string;
  size: keyof typeof frames;
  language?: Language;
  scheme?: "light" | "dark";
  path: string;
  mock: (page: Page) => Promise<unknown>;
  cookies?: { name: string; value: string }[];
  open?: (page: Page) => Promise<unknown>;
}[] = [
  {
    file: "1.13-accept-invitation-en-light-desktop",
    size: "desktop",
    path: acceptPage,
    mock: (page) => mockInvitation(page, omar),
  },
  {
    file: "1.09-terms-of-use-updated-en-light-desktop",
    size: "desktop",
    path: `/${northwind.slug}`,
    mock: asksForTerms(),
  },
  {
    file: "1.10-terms-of-use-updated-no-summary-accepted-box-en-light-desktop",
    size: "desktop",
    path: `/${northwind.slug}`,
    mock: asksForTerms({ ...decemberTerms, summary: null }),
    open: agreeToTerms,
  },
  {
    file: "1.11-terms-of-use-updated-accepting-en-light-desktop",
    size: "desktop",
    path: `/${northwind.slug}`,
    // The acceptance never answers, so the gate stays in its accepting state.
    mock: async (page) => {
      await asksForTerms()(page);
      await page.route("**/api/v1/me/terms/versions/*/accept", () => {});
    },
    open: acceptTerms,
  },
  {
    file: "1.12-terms-of-use-updated-error-en-light-desktop",
    size: "desktop",
    path: `/${northwind.slug}`,
    mock: async (page) => {
      await asksForTerms()(page);
      await page.route("**/api/v1/me/terms/versions/*/accept", (route) =>
        route.abort(),
      );
    },
    open: async (page) => {
      await acceptTerms(page);
      await page.getByText("Couldn’t save your acceptance").waitFor();
    },
  },
  {
    file: "1.14-phone-terms-of-use-updated-en-light-phone",
    size: "phone",
    path: `/${northwind.slug}`,
    mock: asksForTerms(),
  },
  {
    file: "1.55-terms-of-use-updated-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: `/${northwind.slug}`,
    mock: asksForTerms(),
  },
  {
    file: "1.56-phone-terms-of-use-updated-ar-light-phone",
    size: "phone",
    language: Language.Ar,
    path: `/${northwind.slug}`,
    mock: asksForTerms(),
  },
  {
    file: "1.15-accept-invitation-terms-en-light-desktop",
    size: "desktop",
    path: acceptPage,
    mock: (page) => mockInvitation(page, omar, { knownUser: false }),
  },
  {
    file: "1.16-invitation-expired-en-light-desktop",
    size: "desktop",
    path: acceptPage,
    mock: (page) =>
      mockInvitation(page, {
        ...omar,
        state: InvitationResponseState.Expired,
        expiresAt: "2026-10-30T13:32:00Z",
      }),
  },
  {
    file: "1.16-invitation-cancelled-en-light-desktop",
    size: "desktop",
    path: acceptPage,
    mock: (page) =>
      mockInvitation(page, {
        ...omar,
        state: InvitationResponseState.Cancelled,
      }),
  },
  {
    file: "1.17-invitation-for-another-email-en-light-desktop",
    size: "desktop",
    path: acceptPage,
    mock: (page) => mockInvitation(page, ErrorCode.InvitationEmailMismatch),
  },
  {
    file: "1.18-choose-an-organisation-en-light-desktop",
    size: "desktop",
    path: "/",
    mock: async (page) => {
      await mockApi(page);
      await memberOf([harbour, northwind])(page);
    },
    cookies: [{ name: lastOrganisationCookie, value: northwind.slug }],
  },
  {
    file: "1.19-no-access-yet-account-menu-en-light-desktop",
    size: "desktop",
    path: "/",
    mock: async (page) => {
      await mockApi(page);
      await memberOf([])(page);
      await lena()(page);
    },
    open: async (page) => {
      await page.getByRole("button", { name: /Lena Fischer/ }).click();
      await page.getByRole("menu").waitFor();
    },
  },
  ...(
    [
      [
        "1.40-blocked-account-suspended-en-light-desktop",
        "desktop",
        "light",
        Language.En,
      ],
      [
        "1.41-blocked-account-suspended-en-dark-desktop",
        "desktop",
        "dark",
        Language.En,
      ],
      [
        "1.42-phone-blocked-account-suspended-en-light-phone",
        "phone",
        "light",
        Language.En,
      ],
      [
        "1.43-phone-blocked-account-suspended-ar-light-phone",
        "phone",
        "light",
        Language.Ar,
      ],
    ] as const
  ).map(([file, size, scheme, language]) => ({
    file,
    size,
    scheme,
    language,
    path: "/",
    mock: async (page: Page) => {
      await mockApi(page);
      await accountRefused(ErrorCode.UserSuspended)(page);
    },
  })),
  ...(
    [
      [
        "1.44-blocked-account-deleted-en-light-desktop",
        "desktop",
        "light",
        Language.En,
      ],
      [
        "1.45-blocked-account-deleted-en-dark-desktop",
        "desktop",
        "dark",
        Language.En,
      ],
      [
        "1.46-phone-blocked-account-deleted-en-light-phone",
        "phone",
        "light",
        Language.En,
      ],
      [
        "1.47-phone-blocked-account-deleted-ar-light-phone",
        "phone",
        "light",
        Language.Ar,
      ],
    ] as const
  ).map(([file, size, scheme, language]) => ({
    file,
    size,
    scheme,
    language,
    path: "/",
    mock: async (page: Page) => {
      await mockApi(page);
      await accountRefused(ErrorCode.UserDeleted)(page);
    },
  })),
  ...(
    [
      [
        "1.48-refused-join-system-admin-en-light-desktop",
        "desktop",
        "light",
        Language.En,
      ],
      [
        "1.49-refused-join-system-admin-en-dark-desktop",
        "desktop",
        "dark",
        Language.En,
      ],
      [
        "1.50-phone-refused-join-system-admin-en-light-phone",
        "phone",
        "light",
        Language.En,
      ],
      [
        "1.51-phone-refused-join-system-admin-ar-light-phone",
        "phone",
        "light",
        Language.Ar,
      ],
    ] as const
  ).map(([file, size, scheme, language]) => ({
    file,
    size,
    scheme,
    language,
    path: acceptPage,
    mock: async (page: Page) => {
      await mockInvitation(page, omar);
      await page.route("**/api/v1/me/invitations/accept", (route) =>
        route.fulfill({
          status: 422,
          json: {
            status: 422,
            title: "Refused",
            code: ErrorCode.InvitationSystemAdminCannotJoin,
            traceId: "00-e2e",
          },
        }),
      );
    },
    open: async (page: Page) => {
      await page
        .getByRole("button", { name: /^(Accept and continue|اقبل وتابع)$/ })
        .click();
      await page
        .getByRole("heading", { name: /System admins|مسؤولي النظام/ })
        .waitFor();
    },
  })),
  {
    file: "2.13-organisation-suspended-en-light-desktop",
    size: "desktop",
    path: `/${northwind.slug}`,
    mock: async (page) => {
      await mockApi(page);
      await memberOf([
        { ...northwind, status: MyTenantResponseStatus.Suspended },
        harbour,
      ])(page);
    },
  },
  {
    file: "2.14-organisation-closed-en-light-desktop",
    size: "desktop",
    path: `/${northwind.slug}`,
    mock: async (page) => {
      await mockApi(page);
      await memberOf([
        { ...northwind, status: MyTenantResponseStatus.Closed },
        harbour,
      ])(page);
    },
  },
];

/** A shot's language, theme and cookies when it names none: English, light, none. */
const entryDefaults = (shot: (typeof entryShots)[number]) => ({
  ...shot,
  language: shot.language ?? Language.En,
  scheme: shot.scheme ?? "light",
  cookies: shot.cookies ?? [],
});

for (const shot of entryShots.map(entryDefaults))
  test(`Screenshot_Entry_${shot.file}`, async ({ page, context, baseURL }) => {
    await setupClerkTestingToken({ page });
    await page.emulateMedia({ colorScheme: shot.scheme });
    await page.setViewportSize(frames[shot.size]);
    await context.addCookies(
      [{ name: localeCookie, value: shot.language }, ...shot.cookies].map(
        (cookie) => ({ ...cookie, url: baseURL }),
      ),
    );
    await shot.mock(page);

    await page.goto(shot.path);
    await page.getByRole("heading", { level: 1 }).waitFor();
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
    await footerSettled(page);
    await shot.open?.(page);
    await page.mouse.move(0, 0);
    await page.evaluate(
      () =>
        new Promise((done) =>
          requestAnimationFrame(() => requestAnimationFrame(done)),
        ),
    );
    await page.evaluate(() =>
      Promise.all(document.getAnimations().map((each) => each.finished)),
    );

    await page.screenshot({ path: `${folder}/entry-${shot.file}.png` });
  });

// The sign-in frames of the Entry section (1.01 to 1.08, 1.20 to 1.22, 1.35 to 1.39, 1.52). Each shot runs signed out, through Clerk's
// test mode: the setup's address takes the code 424242 and a wrong code gets Clerk's own refusal. Playwright holds, breaks or
// changes Clerk's answers only to reach a state that the real flow passes too quickly (sending), cannot reach without a failure
// (the network) or reaches only with an authenticator app, which the web's user has not (the second factor).
const clerkApi = "**/v1/client/sign_ins**";

/** Clerk answers the first factor as for an account with an authenticator app: the sign-in then asks for the second factor. */
const asksForASecondFactor = (page: Page) =>
  page.route(
    "**/v1/client/sign_ins/*/attempt_first_factor**",
    async (route) => {
      const response = await route.fetch();
      const body = await response.json();
      for (const signIn of [body.response, body.client?.sign_in])
        if (signIn) {
          signIn.status = "needs_second_factor";
          signIn.supported_second_factors = [{ strategy: "totp" }];
        }
      await route.fulfill({ response, json: body });
    },
  );

/** The address a frame shows, typed in the focused field. */
const typeTheFramesAddress =
  (address = "ayse@northwind.example") =>
  (page: Page) =>
    // The page's only field, whose name is in the page's language.
    page.getByRole("textbox").fill(address);

const toTheCodeStep = async (page: Page) => {
  await page.getByRole("textbox", { name: "Email" }).fill(e2eUserEmail());
  await page.getByRole("button", { name: "Continue", exact: true }).click();
  await page.getByRole("heading", { name: "Check your email" }).waitFor();
};

const signInShots: {
  file: string;
  size: keyof typeof frames;
  language?: Language;
  scheme?: "light" | "dark";
  path?: string;
  /** Holds or changes Clerk's answers before the page loads. */
  mock?: (page: Page) => Promise<unknown>;
  open?: (page: Page) => Promise<unknown>;
}[] = [
  {
    file: "1.01-sign-in-en-light-desktop",
    size: "desktop",
    open: typeTheFramesAddress(),
  },
  {
    file: "1.02-sign-in-invalid-email-en-light-desktop",
    size: "desktop",
    open: async (page) => {
      await page.getByRole("textbox", { name: "Email" }).fill("ayse@northwind");
      await page.getByRole("button", { name: "Continue", exact: true }).click();
    },
  },
  {
    file: "1.03-sign-in-sending-en-light-desktop",
    size: "desktop",
    // The code never goes out, so the screen stays in its sending state.
    mock: (page) => page.route(clerkApi, () => {}),
    open: async (page) => {
      await page
        .getByRole("textbox", { name: "Email" })
        .fill("ayse@northwind.example");
      await page.getByRole("button", { name: "Continue", exact: true }).click();
      await page.getByRole("button", { name: "Sending the code…" }).waitFor();
    },
  },
  {
    file: "1.04-sign-in-failed-en-light-desktop",
    size: "desktop",
    // The request that sends the code breaks. Under Playwright Clerk takes a failed request for an offline browser
    // (navigator.webdriver) and answers nothing, so the page says it is no webdriver, as a person's browser does.
    mock: async (page) => {
      await page.addInitScript(() =>
        Object.defineProperty(Navigator.prototype, "webdriver", {
          get: () => false,
        }),
      );
      await page.route(clerkApi, (route) => route.abort());
    },
    open: async (page) => {
      await page
        .getByRole("textbox", { name: "Email" })
        .fill("ayse@northwind.example");
      await page.getByRole("button", { name: "Continue", exact: true }).click();
      // Only the banner: Next's route announcer is an empty alert that is there at once.
      await page.getByText("We could not send the code").waitFor();
    },
  },
  {
    file: "1.05-enter-the-code-en-light-desktop",
    size: "desktop",
    open: toTheCodeStep,
  },
  {
    file: "1.06-code-wrong-en-light-desktop",
    size: "desktop",
    open: async (page) => {
      await toTheCodeStep(page);
      await page
        .getByRole("textbox", { name: "Code" })
        .pressSequentially("381946");
      await page.getByRole("button", { name: "Sign in" }).click();
      await page.getByRole("alert").filter({ hasText: "wrong" }).waitFor();
    },
  },
  {
    file: "1.07-two-step-verification-en-light-desktop",
    size: "desktop",
    mock: asksForASecondFactor,
    open: async (page) => {
      await toTheCodeStep(page);
      await page
        .getByRole("textbox", { name: "Code" })
        .pressSequentially("424242");
      await page.getByRole("button", { name: "Sign in" }).click();
      await page
        .getByRole("heading", { name: "Two-step verification" })
        .waitFor();
    },
  },
  {
    file: "1.08-backup-code-en-light-desktop",
    size: "desktop",
    mock: asksForASecondFactor,
    open: async (page) => {
      await toTheCodeStep(page);
      await page
        .getByRole("textbox", { name: "Code" })
        .pressSequentially("424242");
      await page.getByRole("button", { name: "Sign in" }).click();
      await page
        .getByRole("button", { name: "Use a backup code instead" })
        .click();
      await page
        .getByRole("textbox", { name: "Backup code" })
        .fill("4f7k-2m9q");
    },
  },
  {
    file: "1.20-sign-in-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    open: typeTheFramesAddress(),
  },
  {
    file: "1.21-phone-sign-in-en-light-phone",
    size: "phone",
    open: typeTheFramesAddress(),
  },
  {
    file: "1.22-phone-enter-the-code-en-light-phone",
    size: "phone",
    open: toTheCodeStep,
  },
  {
    file: "1.35-sign-in-language-list-en-light-desktop",
    size: "desktop",
    open: (page) => page.getByRole("combobox").click(),
  },
  {
    file: "1.36-sign-in-no-language-matches-en-light-desktop",
    size: "desktop",
    open: async (page) => {
      await page.getByRole("combobox").click();
      await page.getByPlaceholder("Search languages").fill("Deutsch");
    },
  },
  {
    file: "1.37-enter-the-code-new-code-sent-en-light-desktop",
    size: "desktop",
    open: async (page) => {
      await toTheCodeStep(page);
      await page.getByRole("button", { name: "Send a new code" }).click();
      await page.getByRole("status").filter({ hasText: "new code" }).waitFor();
    },
  },
  // Without Clerk's script the callback never ends, so the card stays as it shows while Clerk works.
  {
    file: "1.38-finishing-sign-in-en-light-desktop",
    size: "desktop",
    path: "/sso-callback",
    mock: (page) => page.route("**/clerk.browser.js", (route) => route.abort()),
  },
  // The same card after 10 seconds: Playwright's clock moves the page's time on.
  {
    file: "1.52-signing-you-in-taking-longer-en-light-desktop",
    size: "desktop",
    path: "/sso-callback",
    mock: async (page) => {
      await page.clock.install();
      await page.route("**/clerk.browser.js", (route) => route.abort());
    },
    open: async (page) => {
      await page.clock.fastForward(10_000);
      await page.getByRole("link", { name: "Try again" }).waitFor();
    },
  },
  // Signed out, the invitation's page signs the person in itself, without the frame's names (the maintainer's decision C).
  {
    file: "1.13-accept-invitation-signed-out-en-light-desktop",
    size: "desktop",
    path: acceptPage,
  },
  {
    file: "1.23-phone-accept-invitation-signed-out-en-light-phone",
    size: "phone",
    path: acceptPage,
  },
  {
    file: "1.39-phone-sign-in-ar-light-phone",
    size: "phone",
    language: Language.Ar,
    open: typeTheFramesAddress("yusuf@al-ufuq.example"),
  },
];

test.describe("signed out", () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  for (const shot of signInShots)
    test(`Screenshot_SignIn_${shot.file}`, async ({
      page,
      context,
      baseURL,
    }) => {
      await setupClerkTestingToken({ page });
      await page.emulateMedia({ colorScheme: shot.scheme ?? "light" });
      await page.setViewportSize(frames[shot.size]);
      await context.addCookies([
        {
          name: localeCookie,
          value: shot.language ?? Language.En,
          url: baseURL,
        },
      ]);
      // The version in force, for the footer's "Terms" (F6: the BFF reads it without a session).
      await page.route("**/api/v1/public/terms", (route) =>
        route.fulfill({ json: termsInForce }),
      );
      await shot.mock?.(page);

      await page.goto(shot.path ?? "/sign-in");
      await page.getByRole("heading", { level: 1 }).waitFor();
      await footerSettled(page);
      await shot.open?.(page);
      // No pointer over a control: a click leaves its hover behind.
      await page.mouse.move(0, 0);
      await page.evaluate(
        () =>
          new Promise((done) =>
            requestAnimationFrame(() => requestAnimationFrame(done)),
          ),
      );
      // An endless animation, such as the busy bar of 1.38, has no end to wait for.
      await page.evaluate(() =>
        Promise.all(
          document
            .getAnimations()
            .filter(
              (each) =>
                each.effect?.getTiming().iterations !==
                Number.POSITIVE_INFINITY,
            )
            .map((each) => each.finished),
        ),
      );

      await page.screenshot({ path: `${folder}/sign-in-${shot.file}.png` });
    });
});
