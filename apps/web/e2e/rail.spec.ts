import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { railCookieName } from "@repo/ui/lib/rail-state";
import { mockMembers, northwind } from "./api";
import { expectAccessible } from "./axe";

// The rail on desktop (F49): the toggle and ⌘B switch it, the cookie keeps it over a reload, and a tablet keeps the icon rail.

const path = `/${northwind.slug}/settings/members`;
const rail = (page: Page) => page.getByRole("navigation", { name: "Main" });
const toggle = (page: Page) =>
  page.getByRole("button", { name: "Menu labels" });
const widthOf = (page: Page) =>
  rail(page).evaluate((element) => element.getBoundingClientRect().width);

/** Reloads once Clerk has loaded and its dev browser cookie is there; an earlier reload can land on sign-in (F13). */
const reload = async (page: Page) => {
  await page.waitForFunction(
    () => (window as { Clerk?: { loaded?: boolean } }).Clerk?.loaded === true,
  );
  await expect
    .poll(async () =>
      (await page.context().cookies()).some((cookie) =>
        cookie.name.startsWith("__clerk_db_jwt"),
      ),
    )
    .toBe(true);
  await page.reload();
};

test.beforeEach(async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockMembers(page);
});

test("Rail_ExpandThenReload_StaysExpanded", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto(path);

  await toggle(page).click();
  await expect(toggle(page)).toHaveAttribute("aria-expanded", "true");
  await reload(page);

  await expect(toggle(page)).toHaveAttribute("aria-expanded", "true");
  await expect(rail(page).getByText("Overview")).toBeVisible();
  expect(await widthOf(page)).toBe(232);
  await expectAccessible(page);
});

test("Rail_CollapseThenReload_StaysCollapsed", async ({
  page,
  context,
  baseURL,
}) => {
  await context.addCookies([
    { name: railCookieName, value: "true", url: baseURL },
  ]);
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto(path);

  await expect(toggle(page)).toHaveAttribute("aria-expanded", "true");
  await page.keyboard.press("ControlOrMeta+b");
  await expect(toggle(page)).toHaveAttribute("aria-expanded", "false");
  await reload(page);

  await expect(toggle(page)).toHaveAttribute("aria-expanded", "false");
  expect(await widthOf(page)).toBe(56);
  await expectAccessible(page);
});

test("Rail_Tablet_KeepsTheIconRailWithoutAToggle", async ({ page }) => {
  await page.setViewportSize({ width: 800, height: 1024 });
  await page.goto(path);

  await expect(rail(page)).toBeVisible();
  await expect(toggle(page)).toBeHidden();
  expect(await widthOf(page)).toBe(56);
  await expectAccessible(page);
});
