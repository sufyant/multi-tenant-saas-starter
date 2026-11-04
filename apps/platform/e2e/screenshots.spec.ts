import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { Language } from "@repo/api-client";
import { localeCookie } from "@repo/ui/lib/locale";
import {
  mockNotifications,
  mockPlatform,
  organisations,
  plans,
  users,
} from "./api";

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
const shellShots: {
  file: string;
  size: keyof typeof frames;
  language?: Language;
  scheme?: "light" | "dark";
  path?: string;
  mock?: (page: Parameters<typeof mockPlatform>[0]) => Promise<unknown>;
  open?: (page: Parameters<typeof mockPlatform>[0]) => Promise<void>;
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
    mock: mockNotifications,
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
    file: "3.08-notifications-en-light-phone",
    size: "phone",
    mock: mockNotifications,
    open: (page) =>
      page.getByRole("button", { name: /^Notifications/ }).click(),
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
