import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { Language } from "@repo/api-client";
import { localeCookie } from "@repo/ui/lib/locale";
import { pseudoCookie } from "@repo/ui/lib/pseudo-locale";
import { mockMembers, northwind } from "./api";
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

test("Shell_EnvironmentStrip_PushesTheLayoutDown", async ({ page }) => {
  await open(page);

  const strip = page.locator("[data-slot=environment-strip]");
  const menu = page.getByRole("navigation", { name: "Main menu" });
  await expect(menu).toBeVisible();
  const stripBox = await strip.boundingBox();
  const menuBox = await menu.boundingBox();
  expect(stripBox).toMatchObject({ y: 0, height: 24 });
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
