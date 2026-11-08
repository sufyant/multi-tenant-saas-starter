import { setupClerkTestingToken } from "@clerk/testing/playwright";
import {
  expect,
  type Locator,
  type Page,
  type Route,
  test,
} from "@playwright/test";
import { Language } from "@repo/api-client";
import { localeCookie } from "@repo/ui/lib/locale";
import { pseudoCookie } from "@repo/ui/lib/pseudo-locale";
import { mockMembers, mockNotifications, northwind } from "./api";
import { expectAccessible } from "./axe";

// The shell of the tenant web (the Shell screens, the Screen sizes rules): a desktop and a phone, light and dark, English and
// Arabic, each checked by axe (F37). The tests run as APP_ENVIRONMENT=local, so the environment strip shows "Local".

const sizes = {
  desktop: { width: 1280, height: 800 },
  phone: { width: 375, height: 812 },
} as const;

const texts = {
  [Language.En]: { menu: "Main menu", open: "Open menu", strip: "Local" },
  [Language.Ar]: {
    menu: "القائمة الرئيسية",
    open: "فتح القائمة",
    strip: "محلي",
  },
} as const;

/** The box of a part of the page, once it shows; a part with no box fails the test. `boundingBox` does not wait. */
const boxOf = async (locator: Locator) => {
  await expect(locator).toBeVisible();
  const box = await locator.boundingBox();
  expect(box).not.toBeNull();
  return box as NonNullable<typeof box>;
};

type Size = keyof typeof sizes;

/** Shows the menu: a desktop has it open; a phone opens the drawer with its menu button. */
const showMenu: Record<
  Size,
  (page: Page, button: string | RegExp) => Promise<void>
> = {
  desktop: async () => {},
  phone: (page, button) => page.getByRole("button", { name: button }).click(),
};
type Shown = keyof typeof texts;

const open = async (
  page: Page,
  {
    size = "desktop",
    language = Language.En,
    path = `/${northwind.slug}`,
    cookies = [],
  }: {
    size?: Size;
    language?: Shown;
    path?: string;
    cookies?: { name: string; value: string }[];
  } = {},
) => {
  await setupClerkTestingToken({ page });
  await page.setViewportSize(sizes[size]);
  await page.context().addCookies(
    [{ name: localeCookie, value: language }, ...cookies].map((cookie) => ({
      ...cookie,
      url: test.info().project.use.baseURL,
    })),
  );
  await mockMembers(page, { language });
  return page.goto(path);
};

for (const size of Object.keys(sizes) as Size[])
  for (const scheme of ["light", "dark"] as const)
    for (const language of [Language.En, Language.Ar] as const)
      test(`Shell_${size}_${scheme}_${language}_ShowsTheMenuAndHasNoAxeViolation`, async ({
        page,
      }) => {
        await page.emulateMedia({ colorScheme: scheme });
        await open(page, { size, language });
        const text = texts[language];

        await expect(page.getByText(text.strip, { exact: true })).toBeVisible();
        await showMenu[size](page, text.open);
        await expect(
          page.getByRole("navigation", { name: text.menu }),
        ).toBeVisible();
        await expectAccessible(page);
      });

// The expanded sidebar from bp-desktop (the Shell screens, 2.01): the organisation's name and the person's role, search inside the
// sidebar, and Notifications as a row with its label. A screen reader's text is 1 px wide, so each check reads the width.
test("Shell_Desktop_SidebarShowsTheOrganisationSearchAndNotificationsRow", async ({
  page,
}) => {
  await open(page);

  const menu = page.getByRole("navigation", { name: "Main menu" });
  await expect(menu).toBeVisible();
  const width = async (text: string) =>
    (await boxOf(menu.getByText(text, { exact: true }))).width;
  expect(await width("Northwind Traders")).toBeGreaterThan(40);
  expect(await width("Owner")).toBeGreaterThan(20);
  expect(await width("Notifications")).toBeGreaterThan(40);
  const menuBox = await boxOf(menu);
  const searchBox = await boxOf(
    menu.getByRole("button", { name: "Search or jump to" }),
  );
  expect(searchBox.x).toBeGreaterThanOrEqual(menuBox.x);
  expect(searchBox.x + searchBox.width).toBeLessThanOrEqual(
    menuBox.x + menuBox.width,
  );
});

// Home (4.01): the welcome with the whole name and a card for each page the role can open.
test("Shell_Home_WelcomesAndOffersThePages", async ({ page }) => {
  await open(page);

  await expect(
    page.getByRole("heading", { level: 1, name: /^Welcome, / }),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: /^Members/ })).toBeVisible();
  await expectAccessible(page);
});

test("Shell_EnvironmentStrip_PushesTheLayoutDown", async ({ page }) => {
  await open(page);

  const strip = page.locator("[data-slot=environment-strip]");
  const menu = page.getByRole("navigation", { name: "Main menu" });
  await expect(menu).toBeVisible();
  const stripBox = await strip.boundingBox();
  const menuBox = await menu.boundingBox();
  expect(stripBox).toMatchObject({ y: 0, height: 24 });
  await expect(strip).toHaveText("Local");
  expect(menuBox?.y).toBeGreaterThanOrEqual(24);
});

// An address that no page has: a real 404 from the server (Next.js documentation, "Streaming", "Status codes"), and "Page not
// found" inside the shell, so the menu stays (the Page not found screens, F59).
test("Shell_MissingPage_IsNotFoundInsideTheShell", async ({ page }) => {
  const response = await open(page, {
    path: `/${northwind.slug}/no-such-page`,
  });

  expect(response?.status()).toBe(404);
  await expect(
    page.getByRole("heading", { name: "Page not found" }),
  ).toBeVisible();
  await expect(
    page.getByRole("navigation", { name: "Main menu" }),
  ).toBeVisible();
  await expectAccessible(page);
});

// 2.43: on a phone every page's content starts 16 px under the top bar, and the empty state's tile 40 px under that (F59).
test("Shell_PhoneMissingPage_PutsTheTile56pxUnderTheTopBar", async ({
  page,
}) => {
  await open(page, { size: "phone", path: `/${northwind.slug}/no-such-page` });

  const bar = await boxOf(page.locator("header").first());
  const tile = await boxOf(
    page.locator("main svg.lucide-search-x").locator(".."),
  );
  expect(tile.y - (bar.y + bar.height)).toBe(56);
});

test("Shell_TabTitle_StartsWithTheEnvironment", async ({ page }) => {
  await open(page);

  await expect(page).toHaveTitle(/^\[Local\] (\(\d+\) )?Home · Starter$/);
});

test("Shell_CommandK_OpensTheCommandMenuWithItsGroups", async ({ page }) => {
  await open(page);
  await expect(
    page.getByRole("navigation", { name: "Main menu" }),
  ).toBeVisible();

  await page.keyboard.press("ControlOrMeta+k");

  const dialog = page.getByRole("dialog", { name: "Command menu" });
  await expect(dialog.getByText("Go to", { exact: true })).toBeVisible();
  await expect(dialog.getByText("Settings · Organisation")).toBeVisible();
  await expectAccessible(page);
});

test("Shell_SettingsMenu_ShowsBackAndTheOrganisationsPages", async ({
  page,
}) => {
  await open(page, { path: `/${northwind.slug}/settings/members` });

  const menu = page.getByRole("navigation", { name: "Settings menu" });
  await expect(
    menu.getByRole("link", { name: "Back to Northwind Traders" }),
  ).toBeVisible();
  await expect(menu.getByRole("link", { name: "Activity log" })).toBeVisible();
  await expect(menu.getByRole("link", { name: "Members" })).toHaveAttribute(
    "aria-current",
    "page",
  );
  // Settings holds only the organisation's pages; the person's pages open from the account menu (F59).
  await expect(menu.getByText("My account")).toHaveCount(0);
  await expect(menu.getByRole("link", { name: "Profile" })).toHaveCount(0);
  await expect(
    menu.getByRole("link", { name: "Account and security" }),
  ).toHaveCount(0);
  await expectAccessible(page);
});

for (const [name, item, path] of [
  ["Profile", "Profile", "/settings/profile"],
  ["AccountAndSecurity", "Account and security", "/settings/account"],
] as const)
  test(`Shell_AccountMenuInSettings_Opens${name}`, async ({ page }) => {
    await open(page, { path: `/${northwind.slug}/settings/members` });

    // The members' list has "Actions for Ayşe Yılmaz" too, so the account button's name is exact.
    await page
      .getByRole("button", { name: "Ayşe Yılmaz", exact: true })
      .click();
    await page.getByRole("menuitem", { name: item }).click();

    await expect(page).toHaveURL(path);
    await expect(
      page.getByRole("heading", { level: 1, name: item }),
    ).toBeVisible();
    await expectAccessible(page);
  });

// The Dialogs on a phone pattern: a form you type into fills the screen, its main button at the foot.
test("Shell_PhoneForm_IsAFullScreenPage", async ({ page }) => {
  await open(page, {
    size: "phone",
    path: `/${northwind.slug}/settings/members?invite=1`,
  });

  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toBeVisible();
  await dialog.evaluate((element) =>
    Promise.all(element.getAnimations().map((each) => each.finished)),
  );
  expect(await dialog.boundingBox()).toMatchObject({
    x: 0,
    width: sizes.phone.width,
  });
  await expect(dialog.getByRole("button", { name: "Cancel" })).toBeVisible();
  await expectAccessible(page);
});

// The Long text pattern: every text 40% longer with accents; the page never scrolls sideways and no button is cut.
for (const size of Object.keys(sizes) as Size[])
  test(`Shell_PseudoLocale_${size}_NothingOverflows`, async ({ page }) => {
    await open(page, {
      size,
      path: `/${northwind.slug}/settings/members`,
      cookies: [{ name: pseudoCookie, value: "1" }],
    });
    await showMenu[size](page, /^\[Øþéñ/);
    await expect(page.getByText(/^\[Ɓåçǩ ţø/).first()).toBeVisible();

    const overflow = await page.evaluate(() => ({
      sideways: document.documentElement.scrollWidth > window.innerWidth,
      cut: [...document.querySelectorAll("main button")]
        .filter((button) => button.scrollWidth > button.clientWidth + 1)
        .map((button) => button.textContent),
    }));
    expect(overflow).toEqual({ sideways: false, cut: [] });
    await expectAccessible(page);
  });

// The tenant web has no page of notifications: on a phone the bell opens its full screen (3.14), whose tabs' row with Mark all
// as read holds the longer texts.
test("Shell_PseudoLocale_NotificationsFullScreen_NothingOverflows", async ({
  page,
}) => {
  await mockNotifications(page);
  await open(page, {
    size: "phone",
    cookies: [{ name: pseudoCookie, value: "1" }],
  });
  await page.getByRole("button", { name: /^\[Ñøţîƒîçåţîøñš/ }).click();
  const screen = page.getByRole("dialog");
  await expect(screen.getByRole("button", { name: /^\[Ṁåŕǩ/ })).toBeVisible();

  const overflow = await screen.evaluate((dialog) => {
    const row = dialog.querySelector("[role=tablist]")
      ?.parentElement as HTMLElement;
    return {
      sideways: document.documentElement.scrollWidth > window.innerWidth,
      row: row.scrollWidth > row.clientWidth,
      cut: [...dialog.querySelectorAll("button")]
        .filter((button) => button.scrollWidth > button.clientWidth + 1)
        .map((button) => button.textContent),
    };
  });
  expect(overflow).toEqual({ sideways: false, row: false, cut: [] });
  await expectAccessible(page);
});

// The browser's offline signal is only a hint (the Connection bar): Chrome on macOS can say offline with some VPNs while every
// call answers. The bar shows nothing, and the offline event's try reads the page again and finds the server.
test("Shell_BrowserSaysOfflineWhileTheCallsAnswer_ShowsNoConnectionBar", async ({
  page,
}) => {
  await page.addInitScript(() =>
    Object.defineProperty(Navigator.prototype, "onLine", { get: () => false }),
  );
  await open(page);
  await expect(
    page.getByRole("navigation", { name: "Main menu" }),
  ).toBeVisible();

  await expect(async () => {
    const read = page.waitForResponse("**/api/v1/me", { timeout: 1000 });
    await page.evaluate(() => window.dispatchEvent(new Event("offline")));
    await read;
  }).toPass();

  await expect(page.locator("[data-slot=connection-bar]")).toHaveCount(0);
});

// Really offline: the calls find no server, so the bar says so; once a call answers again it says "Back online". The route
// fails each call too, because the page's mocked answers would otherwise come without a network.
test("Shell_Offline_ShowsTheConnectionBarThenBackOnline", async ({ page }) => {
  await open(page);
  await expect(
    page.getByRole("navigation", { name: "Main menu" }),
  ).toBeVisible();
  const bar = page.locator("[data-slot=connection-bar]");
  const noNetwork = (route: Route) => route.abort("internetdisconnected");

  await page.route("**/api/v1/**", noNetwork);
  await page.context().setOffline(true);
  await expect(bar).toContainText("You’re offline");

  await page.unroute("**/api/v1/**", noNetwork);
  await page.context().setOffline(false);
  await expect(bar).toContainText("Back online", { timeout: 10_000 });
});
