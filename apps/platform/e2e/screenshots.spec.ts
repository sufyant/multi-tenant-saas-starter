import { randomUUID } from "node:crypto";
import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { ErrorCode, Language, NotificationType } from "@repo/api-client";
import { localeCookie } from "@repo/ui/lib/locale";
import {
  mockNotifications,
  mockPlatform,
  mockRename,
  organisations,
  plans,
  shannonSuspended,
  users,
} from "./api";
import { createUser, deleteUser } from "./clerk-users";
import { mockIdentity } from "./identity";
import { enrolledUser, signInWithEmailCode, totpCode } from "./two-step";

/** The bell's tabs; a page may have tabs of its own. */
const bellTabs = (page: Page) =>
  page.getByRole("tablist", { name: "Which notifications" });

// Screenshots for a pull request. They run only with E2E_SCREENSHOTS=1 and write to apps/platform/test-results/screenshots,
// which Git ignores and Playwright empties at the start of each run. Attach them to the pull request; never commit them.
test.skip(
  !process.env.E2E_SCREENSHOTS,
  "Set E2E_SCREENSHOTS=1 to take the screenshots.",
);

const folder = "test-results/screenshots";
const sizes = {
  desktop: { width: 1440, height: 900 },
  tablet: { width: 834, height: 1112 },
  phone: { width: 390, height: 844 },
};
const variants = [
  { language: Language.En, scheme: "light", size: "desktop" },
  { language: Language.En, scheme: "dark", size: "desktop" },
  { language: Language.Tr, scheme: "light", size: "tablet" },
  { language: Language.Ar, scheme: "light", size: "phone" },
] as const;

// The platform's sections: Organisations, Users, System admins, Plans and Activity, with one organisation, one user and one plan.
const pages = [
  ["organisations", "/"],
  ["organisation", `/organisations/${organisations.atlas.id}`],
  ["users", "/users"],
  ["user", `/users/${users.ayse.id}`],
  ["system-admins", "/settings/system-admins"],
  ["plans", "/plans"],
  ["plan", `/plans/${plans.pro.id}`],
  ["plan-default", `/plans/${plans.free.id}`],
  ["plan-new", "/plans/new"],
  ["organisation-plan", `/organisations/${organisations.northwind.id}`],
  ["activity", "/activity"],
] as const;

for (const { language, scheme, size } of variants)
  for (const [name, path] of pages)
    test(`Screenshot_${name}-${language}-${scheme}-${size}`, async ({
      page,
      context,
      baseURL,
    }) => {
      await setupClerkTestingToken({ page });
      await page.emulateMedia({ colorScheme: scheme });
      await page.setViewportSize(sizes[size]);
      // The language's cookie (F13), so the first render is already in it.
      await context.addCookies([
        { name: localeCookie, value: language, url: baseURL },
      ]);
      await mockPlatform(page, language);

      await page.goto(path);
      await page.getByRole("main").getByRole("heading").first().waitFor();
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);

      await page.screenshot({
        path: `${folder}/${name}-${language}-${scheme}-${size}.png`,
        fullPage: true,
      });
    });

// The account menu open: English light, Arabic dark and a phone, where it opens from the drawer.
const menus = [
  {
    language: Language.En,
    scheme: "light",
    size: "desktop",
    name: "Sam Kelly",
  },
  { language: Language.Ar, scheme: "dark", size: "desktop", name: "Sam Kelly" },
  { language: Language.En, scheme: "light", size: "phone", name: "Sam Kelly" },
] as const;

for (const { language, scheme, size, name } of menus)
  test(`Screenshot_user-menu-${language}-${scheme}-${size}`, async ({
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
    await mockPlatform(page, language);

    await page.goto("/users");
    await page.getByRole("main").getByRole("heading").first().waitFor();
    if (size === "phone")
      await page.getByRole("button", { name: "Open menu" }).click();
    await page.getByRole("button", { name, exact: true }).click();
    await page.getByRole("menu").waitFor();

    await page.screenshot({
      path: `${folder}/user-menu-${language}-${scheme}-${size}.png`,
    });
  });

// The platform's bell and its list (the Notifications boards), and Settings › Notifications in Arabic.
test("Screenshot_Notifications_EnglishLightDesktop", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "light" });
  await page.setViewportSize({ width: 1440, height: 900 });
  await mockPlatform(page);
  await mockNotifications(page);

  await page.goto("/");
  await page.getByRole("button", { name: "Notifications, 1 unread" }).click();
  await page.getByRole("link", { name: /was suspended/ }).waitFor();

  await page.screenshot({
    path: `${folder}/notifications-en-light-desktop.png`,
  });
});

// The shell at the sizes of the design's frames (the platform's Shell and Notifications screens), for the comparison in each pull
// request: CI uploads them as the workflow artifact "screenshots" (scripts/ci/test-e2e.sh). The name gives the frame.
const frames = {
  desktop: { width: 1280, height: 800 },
  tablet: { width: 1024, height: 768 },
  phone: { width: 375, height: 812 },
};
/** The platform's Notifications frames: two unread items and one read item, each with its area and the person who acted. */
const ago = (minutes: number) =>
  new Date(Date.now() - minutes * 60_000).toISOString();
const bellList = {
  unread: 2,
  items: [
    { ...shannonSuspended, createdAt: ago(20) },
    {
      ...shannonSuspended,
      id: "0198f0a2-0000-7000-8000-0000000000b2",
      type: NotificationType.TenantOwnerJoined,
      title: "Northwind Traders has its Owner",
      actorName: "Ayşe Demir",
      tenantName: "Northwind Traders",
      createdAt: ago(24 * 60),
    },
    {
      ...shannonSuspended,
      id: "0198f0a2-0000-7000-8000-0000000000b3",
      type: NotificationType.TenantClosed,
      title: "Fernwood Studio was closed",
      actorName: null,
      tenantName: "Fernwood Studio",
      createdAt: ago(3 * 24 * 60),
      readAt: ago(2 * 24 * 60),
      unread: false,
    },
  ],
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
    googleEmail: "deniz.kaya@gmail.example",
    otherSessions: true,
  });
/** Waits until the sessions show the last of the other devices. */
const otherSessionsShown = (page: Page) =>
  page.getByText("Firefox on Windows", { exact: true }).waitFor();

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

const shellShots: {
  file: string;
  size: keyof typeof frames;
  language?: Language;
  scheme?: "light" | "dark";
  path?: string;
  mock?: (page: Parameters<typeof mockPlatform>[0]) => Promise<unknown>;
  open?: (page: Parameters<typeof mockPlatform>[0]) => Promise<void>;
  /** The whole page, for a frame taller than the screen. */
  fullPage?: boolean;
  /** What the page shows once it has loaded, when it is not a heading of the content. */
  ready?: (page: Parameters<typeof mockPlatform>[0]) => Promise<void>;
}[] = [
  { file: "2.01-organisations-en-light-desktop", size: "desktop" },
  {
    file: "2.13-organisations-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
  },
  {
    file: "2.01-organisations-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
  },
  { file: "2.01-organisations-en-light-tablet", size: "tablet" },
  {
    file: "2.14-drawer-en-light-phone",
    size: "phone",
    open: (page) => page.getByRole("button", { name: "Open menu" }).click(),
  },
  {
    file: "2.02-command-en-light-desktop",
    size: "desktop",
    open: (page) => page.keyboard.press("ControlOrMeta+k"),
  },
  {
    file: "2.03-account-menu-en-light-desktop",
    size: "desktop",
    open: (page) =>
      page.getByRole("button", { name: "Sam Kelly", exact: true }).click(),
  },
  {
    file: "3.01-notifications-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, bellList),
    open: (page) =>
      page.getByRole("button", { name: /^Notifications/ }).click(),
  },
  {
    file: "3.03-notifications-empty-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, { items: [], unread: 0 }),
    open: (page) =>
      page.getByRole("button", { name: /^Notifications/ }).click(),
  },
  {
    file: "3.02-notifications-all-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, bellList),
    open: async (page) => {
      await page.getByRole("button", { name: /^Notifications/ }).click();
      await bellTabs(page).getByRole("tab", { name: "All" }).click();
      await page.getByRole("link", { name: /was suspended/ }).waitFor();
    },
  },
  // On a phone the bell opens the page of notifications under the top bar (3.08).
  {
    file: "3.08-notifications-en-light-phone",
    size: "phone",
    path: "/notifications",
    mock: (page) => mockNotifications(page, bellList),
    // The top bar names the page; the content has no heading of its own (3.08).
    ready: (page) =>
      page
        .getByRole("main")
        .getByRole("link", { name: /suspended/ })
        .waitFor(),
  },
  {
    file: "3.10-notifications-empty-en-light-phone",
    size: "phone",
    path: "/notifications",
    mock: (page) => mockNotifications(page, { items: [], unread: 0 }),
    ready: (page) =>
      page.getByRole("main").getByText("No notifications").waitFor(),
  },
  {
    file: "2.22-not-found-en-light-desktop",
    size: "desktop",
    path: "/no-such-page",
  },
  {
    file: "2.25-not-found-en-light-phone",
    size: "phone",
    path: "/no-such-page",
  },
  {
    file: "10-settings-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
  },
  {
    file: "2.15-settings-drawer-en-light-phone",
    size: "phone",
    path: "/settings/system-admins",
    open: (page) => page.getByRole("button", { name: "Open menu" }).click(),
  },
  // The theme's card under the Profile form (36-design): System on a light device, Dark chosen in the dark frame. Each shot
  // scrolls the card into view, because the two name fields make the form taller than the frame's.
  {
    file: "8.01-profile-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "8.08-profile-en-light-tablet",
    size: "tablet",
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "8.07-profile-en-light-phone",
    size: "phone",
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "8.09-profile-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/settings/profile",
    open: async (page) => {
      await page.getByRole("radio", { name: "Dark" }).click();
      await showTheme(page);
    },
  },
  {
    file: "8.10-profile-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "8.02-profile-edited-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    open: editName,
  },
  {
    file: "8.03-profile-name-empty-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    open: emptyName,
  },
  {
    file: "8.04-profile-saved-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    mock: (page) => mockRename(page, Language.En),
    open: saveName,
  },
  // Account and security (F62): 9.01, 9.13 and 9.14 show Google connected and two other sessions. The setup's temporary user has
  // one session and no linked account, so Clerk's answers are changed on the way to the page.
  {
    file: "9.01-account-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: otherSessionsShown,
  },
  {
    file: "9.14-account-en-dark-desktop",
    fullPage: true,
    size: "desktop",
    scheme: "dark",
    path: "/settings/account",
    mock: fullState,
    open: otherSessionsShown,
  },
  {
    file: "9.13-account-en-light-phone",
    fullPage: true,
    size: "phone",
    path: "/settings/account",
    mock: fullState,
    open: otherSessionsShown,
  },
  {
    file: "9.04-change-email-en-light-desktop",
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: changeEmail(),
  },
  {
    file: "9.05-email-code-en-light-desktop",
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: changeEmail("deniz.kaya@platform.example"),
  },
];

/** A shot's language, theme and page when it names none: English, light, the start page. */
const withDefaults = (shot: (typeof shellShots)[number]) => ({
  ...shot,
  language: shot.language ?? Language.En,
  scheme: shot.scheme ?? "light",
  path: shot.path ?? "/",
});

for (const shot of shellShots.map(withDefaults))
  test(`Screenshot_Shell_${shot.file}`, async ({ page, context, baseURL }) => {
    await setupClerkTestingToken({ page });
    await page.emulateMedia({ colorScheme: shot.scheme });
    await page.setViewportSize(frames[shot.size]);
    await context.addCookies([
      { name: localeCookie, value: shot.language, url: baseURL },
    ]);
    await mockPlatform(page, shot.language);
    await shot.mock?.(page);

    await page.goto(shot.path);
    await (shot.ready?.(page) ??
      page.getByRole("main").getByRole("heading").first().waitFor());
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

    await page.screenshot({
      path: `${folder}/shell-${shot.file}.png`,
      fullPage: shot.fullPage,
    });
  });

// The platform's Entry frames (1.01 to 1.12). The sign-in shots run signed out with the setup's user, who has an authenticator
// app, so Clerk asks for the second factor on its own. The enrolment shots sign in a user of their own, made for the shot and
// deleted after it, because only a user without an authenticator app reaches the enrolment. Playwright changes one answer of
// Clerk: the locked account of 1.07, which a real lock would keep for the setup's user for the rest of the run.
const toTheCode = async (page: Page) => {
  await page.getByRole("textbox", { name: "Email" }).fill(enrolledUser().email);
  await page.getByRole("button", { name: "Continue", exact: true }).click();
  await page.getByRole("heading", { name: "Check your email" }).waitFor();
};

/** The address the frames show, typed in the focused field. */
const typeTheFramesAddress = (page: Page) =>
  page.getByRole("textbox", { name: "Email" }).fill("deniz@platform.example");

const toTheSecondFactor = async (page: Page) => {
  await toTheCode(page);
  await page.getByRole("textbox", { name: "Code" }).pressSequentially("424242");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page
    .getByRole("heading", { name: "Enter the code from your app" })
    .waitFor();
};

const entryShots: {
  file: string;
  size: keyof typeof frames;
  scheme?: "light" | "dark";
  path?: string;
  mock?: (page: Page) => Promise<unknown>;
  open?: (page: Page) => Promise<unknown>;
}[] = [
  {
    file: "1.01-sign-in-en-light-desktop",
    size: "desktop",
    open: typeTheFramesAddress,
  },
  {
    file: "1.02-check-your-email-en-light-desktop",
    size: "desktop",
    open: toTheCode,
  },
  {
    file: "1.05-authenticator-code-en-light-desktop",
    size: "desktop",
    open: toTheSecondFactor,
  },
  {
    file: "1.06-backup-code-en-light-desktop",
    size: "desktop",
    open: async (page) => {
      await toTheSecondFactor(page);
      await page
        .getByRole("button", { name: "Use a backup code instead" })
        .click();
      await page
        .getByRole("textbox", { name: "Backup code" })
        .fill("4f7k-2m9q");
    },
  },
  {
    file: "1.07-wrong-code-locked-en-light-desktop",
    size: "desktop",
    mock: (page) =>
      page.route("**/v1/client/sign_ins/*/attempt_first_factor**", (route) =>
        route.fulfill({
          status: 403,
          json: {
            errors: [{ code: "user_locked", message: "", long_message: "" }],
          },
        }),
      ),
    open: async (page) => {
      await toTheCode(page);
      await page
        .getByRole("textbox", { name: "Code" })
        .pressSequentially("381946");
      await page.getByRole("button", { name: "Sign in" }).click();
      await page.getByRole("alert").filter({ hasText: "Too many" }).waitFor();
    },
  },
  {
    file: "1.09-sign-in-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    open: typeTheFramesAddress,
  },
  {
    file: "1.10-phone-sign-in-en-light-phone",
    size: "phone",
    open: typeTheFramesAddress,
  },
  {
    file: "1.11-phone-check-your-email-en-light-phone",
    size: "phone",
    open: toTheCode,
  },
  {
    file: "1.12-phone-two-step-verification-en-light-phone",
    size: "phone",
    open: toTheSecondFactor,
  },
  // Without Clerk's script the callback never ends, so the card stays as it shows while Clerk works.
  {
    file: "1.13-signing-you-in-en-light-desktop",
    size: "desktop",
    path: "/sso-callback",
    mock: (page) => page.route("**/clerk.browser.js", (route) => route.abort()),
  },
  // The same card after 10 seconds: Playwright's clock moves the page's time on.
  {
    file: "1.14-signing-you-in-taking-longer-en-light-desktop",
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
];

/** The screen at rest: no pointer over a control, and every opening animation has finished. */
const still = async (page: Page) => {
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
};

test.describe("signed out", () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  for (const shot of entryShots.map((each) => ({
    ...each,
    path: each.path ?? "/sign-in",
  })))
    test(`Screenshot_Entry_${shot.file}`, async ({
      page,
      context,
      baseURL,
    }) => {
      await setupClerkTestingToken({ page });
      await page.emulateMedia({ colorScheme: shot.scheme ?? "light" });
      await page.setViewportSize(frames[shot.size]);
      await context.addCookies([
        { name: localeCookie, value: Language.En, url: baseURL },
      ]);
      await shot.mock?.(page);

      await page.goto(shot.path);
      await page.getByRole("heading", { level: 1 }).waitFor();
      await shot.open?.(page);
      await still(page);

      await page.screenshot({ path: `${folder}/entry-${shot.file}.png` });
    });

  // Frames 1.03 and 1.04: a user without an authenticator app signs in, and the gate asks them to set one up.
  test("Screenshot_Entry_1.03-and-1.04-enrolment-en-light-desktop", async ({
    page,
  }) => {
    test.setTimeout(120_000);
    const email = `e2e-platform-${randomUUID()}+clerk_test@example.com`;
    const id = await createUser(email);
    try {
      await setupClerkTestingToken({ page });
      await page.emulateMedia({ colorScheme: "light" });
      await page.setViewportSize(frames.desktop);
      await page.goto("/sign-in");
      await signInWithEmailCode(page, email);
      await page
        .getByRole("heading", { name: "Set up two-step verification" })
        .waitFor({ timeout: 30_000 });
      const key = page.getByText(/^[A-Z2-7]{4}( [A-Z2-7]{1,4})+$/);
      await still(page);
      await page.screenshot({
        path: `${folder}/entry-1.03-set-up-the-authenticator-en-light-desktop.png`,
      });

      const secret = (await key.innerText()).replaceAll(" ", "");
      await page
        .getByRole("textbox", { name: "Enter the 6-digit code the app shows" })
        .pressSequentially(await totpCode(secret));
      await page
        .getByRole("button", { name: "Turn on two-step sign-in" })
        .click();
      await expect(
        page.getByRole("list", { name: "Backup codes" }).getByRole("listitem"),
      ).toHaveCount(10);
      await still(page);
      await page.screenshot({
        path: `${folder}/entry-1.04-save-backup-codes-en-light-desktop.png`,
      });
    } finally {
      await deleteUser(id);
    }
  });
});

// Frame 1.08: a signed-in person whom the door refuses.
test("Screenshot_Entry_1.08-not-a-system-admin-en-light-desktop", async ({
  page,
}) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "light" });
  await page.setViewportSize(frames.desktop);
  await page.route("**/api/v1/me", (route) =>
    route.fulfill({
      json: {
        id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e31",
        email: "maria@northwind.example",
        name: "Maria Rossi",
        isSystemAdmin: false,
        language: Language.En,
        timeZone: "Europe/Dublin",
      },
    }),
  );
  await page.route("**/api/v1/platform/tenants**", (route) =>
    route.fulfill({
      status: 403,
      json: { status: 403, title: "Forbidden", code: ErrorCode.AuthForbidden },
    }),
  );

  await page.goto("/");
  await page
    .getByRole("heading", { name: "You don’t have access to the platform" })
    .waitFor();
  // The address comes from Clerk's user, which loads after the page.
  await page.locator("bdi").filter({ hasText: "@" }).waitFor();
  await still(page);

  await page.screenshot({
    path: `${folder}/entry-1.08-not-a-system-admin-en-light-desktop.png`,
  });
});
