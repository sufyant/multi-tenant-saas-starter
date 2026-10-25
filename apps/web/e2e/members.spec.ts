import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Locator, type Page, test } from "@playwright/test";
import { BuiltInRole, Language } from "@repo/api-client";
import { mockMembers, northwind } from "./api";
import { expectAccessible } from "./axe";

const page = `/${northwind.slug}/settings/members`;

const open = async (
  browser: Page,
  options: Parameters<typeof mockMembers>[1] = {},
  path = page,
) => {
  await setupClerkTestingToken({ page: browser });
  await mockMembers(browser, options);
  await browser.goto(path);
};

const rowMenu = (browser: Page, name: string) =>
  browser
    .getByRole("table", { name: "Members" })
    .getByRole("button", { name: `Actions for ${name}` });

test("Members_Owner_SeesTheListTheTabsAndInvite", async ({ page: browser }) => {
  await open(browser);

  await expect(
    browser.getByRole("heading", { level: 1, name: "Members" }),
  ).toBeVisible();
  await expect(
    browser.getByRole("button", { name: "Invite member" }),
  ).toBeVisible();
  await expect(browser.getByRole("tab", { name: "Invitations" })).toBeVisible();
  await expect(
    browser.getByRole("table", { name: "Members" }).getByText("John Byrne"),
  ).toBeVisible();
  await expectAccessible(browser);
});

test("Members_Member_SeesAReadOnlyList", async ({ page: browser }) => {
  await open(browser, { role: BuiltInRole.Member });

  await expect(
    browser.getByRole("table", { name: "Members" }).getByText("John Byrne"),
  ).toBeVisible();
  await expect(
    browser.getByRole("button", { name: "Invite member" }),
  ).toHaveCount(0);
  await expect(browser.getByRole("tablist")).toHaveCount(0);
  await expect(rowMenu(browser, "John Byrne")).toHaveCount(0);
  await expectAccessible(browser);
});

test("Members_InviteAMember_SendsItAndSaysSo", async ({ page: browser }) => {
  await open(browser);

  await browser.getByRole("button", { name: "Invite member" }).click();
  const dialog = browser.getByRole("alertdialog", { name: "Invite a member" });
  await expect(dialog).toBeVisible();
  await expectAccessible(browser);
  await dialog
    .getByRole("textbox", { name: "Email" })
    .fill("khalid@northwind.io");
  await dialog.getByRole("button", { name: "Send invitation" }).click();

  await expect(
    browser.getByText("Invitation sent", { exact: true }),
  ).toBeVisible();
  // The date is the mock's expiresAt, seven days from now.
  await expect(
    browser.getByText(
      /^khalid@northwind\.io can join until \d{1,2} [A-Z][a-z]{2} \d{4}\.$/,
    ),
  ).toBeVisible();
});

test("Members_OpenTheUserMenu_ShowsItOverTheTopBar", async ({
  page: browser,
}) => {
  await open(browser);

  await browser.getByRole("button", { name: "AY Account" }).click();
  const menu = browser.getByRole("menu");
  await expect(menu).toBeVisible();

  // jsdom cannot stack layers: the browser answers which element is on top just under the menu's top edge (F53). The point is
  // taken once the opening animation has ended.
  const onTop = await menu.evaluate(async (element) => {
    await Promise.all(element.getAnimations().map((each) => each.finished));
    const box = element.getBoundingClientRect();
    return element.contains(
      document.elementFromPoint(box.x + box.width / 2, box.y + 4),
    );
  });
  expect(onTop).toBe(true);
  await expectAccessible(browser);
});

/** Opens the user menu and waits for its opening animation, so the boxes are the final ones. */
const openUserMenu = async (browser: Page, name: string) => {
  const avatar = browser.getByRole("button", { name });
  await avatar.click();
  const menu = browser.getByRole("menu");
  await menu.evaluate((element) =>
    Promise.all(element.getAnimations().map((each) => each.finished)),
  );
  return { avatar, menu };
};

/** The drawn box of an element. */
const rect = (locator: Locator) =>
  locator.evaluate((element) =>
    element.getBoundingClientRect().toJSON(),
  ) as Promise<DOMRect>;

/**
 * Base UI's positioner rounds the menu to whole device pixels. The avatar is centred in the top bar's 51px under its border, so
 * its bottom edge sits on a half pixel.
 */
const pixel = 0.5;

/** A token's value as the browser draws it: a colour as `rgb(...)`, a size in pixels. */
const token = (browser: Page, name: string, property: "color" | "width") =>
  browser.evaluate(
    ([name, property]) => {
      const probe = document.createElement("div");
      probe.style.setProperty(property, `var(--${name})`);
      document.body.append(probe);
      const value = getComputedStyle(probe).getPropertyValue(property);
      probe.remove();
      return value;
    },
    [name, property] as const,
  );

// jsdom has no layout. The browser measures the boxes and the drawn colours against the tokens, so a drift from the UserMenu
// boards fails here.
test("Members_UserMenu_OpensSixPixelsBelowTheAvatarAtItsEndEdge", async ({
  page: browser,
}) => {
  await open(browser);

  const { avatar, menu } = await openUserMenu(browser, "AY Account");
  const trigger = await rect(avatar);
  const box = await rect(menu);
  expect(Math.abs(box.top - (trigger.bottom + 6))).toBeLessThanOrEqual(pixel);
  expect(box.right).toBeCloseTo(trigger.right, 0);
  expect(box.width).toBe(280);
  await expectAccessible(browser);
});

test("Members_UserMenuInArabic_AlignsToTheAvatarsLeftEdge", async ({
  page: browser,
}) => {
  await open(browser, { language: Language.Ar });

  const { avatar, menu } = await openUserMenu(browser, "AY الحساب");
  const trigger = await rect(avatar);
  const box = await rect(menu);
  expect(Math.abs(box.top - (trigger.bottom + 6))).toBeLessThanOrEqual(pixel);
  expect(box.left).toBeCloseTo(trigger.left, 0);
  await expectAccessible(browser);
});

test("Members_UserMenuOnAPhone_FitsTheScreen", async ({ page: browser }) => {
  await browser.setViewportSize({ width: 390, height: 844 });
  await open(browser);

  const { avatar, menu } = await openUserMenu(browser, "AY Account");
  const box = await rect(menu);
  expect(box.left).toBeGreaterThanOrEqual(16);
  expect(box.right).toBeLessThanOrEqual(390 - 16);
  expect(`${(await rect(avatar)).width}px`).toBe(
    await token(browser, "control-lg", "width"),
  );
  await expectAccessible(browser);
});

test("Members_Avatar_IsAControlHighNeutralCircle", async ({
  page: browser,
}) => {
  await open(browser);

  const avatar = browser.getByRole("button", { name: "AY Account" });
  await expect(avatar).toBeVisible();
  expect(`${(await rect(avatar)).width}px`).toBe(
    await token(browser, "control-md", "width"),
  );
  const fill = await avatar
    .locator('[data-slot="avatar-fallback"]')
    .evaluate((element) => getComputedStyle(element).backgroundColor);
  expect(fill).toBe(await token(browser, "neutral-text", "color"));
});

test("Members_ChosenTheme_SitsOnPrimarySubtle", async ({ page: browser }) => {
  await open(browser);

  await openUserMenu(browser, "AY Account");
  const chosen = browser.getByRole("menuitemradio", { checked: true });
  const background = await chosen.evaluate(
    (element) => getComputedStyle(element).backgroundColor,
  );
  expect(background).toBe(await token(browser, "primary-subtle", "color"));
});

test("Members_RoleFilter_IsAsHighAsTheSearchField", async ({
  page: browser,
}) => {
  await open(browser);

  const filter = browser.getByRole("combobox", { name: "Filter by role" });
  await expect(filter).toHaveText("All roles");
  const search = browser.getByRole("searchbox", {
    name: "Search by name or email",
  });
  expect((await rect(filter)).height).toBe((await rect(search)).height);
  await expectAccessible(browser);
});

test("Members_OpenADialog_PutsItsBackdropOverTheTopBar", async ({
  page: browser,
}) => {
  await open(browser);

  await browser.getByRole("button", { name: "Invite member" }).click();
  await expect(
    browser.getByRole("alertdialog", { name: "Invite a member" }),
  ).toBeVisible();

  // The element on top at the top bar's start is the backdrop, not the bar (F53). The bar's centre is under the dialog itself,
  // which is centred. The open dialog hides the bar from the accessibility tree, so no role finds it.
  const onTop = await browser.locator("header").evaluate((bar) => {
    const box = bar.getBoundingClientRect();
    return document
      .elementFromPoint(box.x + 8, box.y + box.height / 2)
      ?.getAttribute("data-slot");
  });
  expect(onTop).toBe("alert-dialog-overlay");
  await expectAccessible(browser);
});

test("Members_ChangeARole_ChangesItAndSaysSo", async ({ page: browser }) => {
  await open(browser);

  await rowMenu(browser, "John Byrne").click();
  await browser.getByRole("menuitem", { name: "Change role" }).click();
  const dialog = browser.getByRole("alertdialog", {
    name: "Change John Byrne’s role",
  });
  await dialog.getByRole("radio", { name: "Admin" }).check();
  await expectAccessible(browser);
  await dialog.getByRole("button", { name: "Change role" }).click();

  await expect(browser.getByText("John Byrne is now an Admin.")).toBeVisible();
});

test("Members_RemoveAMember_RemovesItAndSaysSo", async ({ page: browser }) => {
  await open(browser);

  await rowMenu(browser, "John Byrne").click();
  await browser
    .getByRole("menuitem", { name: "Remove from organisation" })
    .click();
  const dialog = browser.getByRole("alertdialog", {
    name: "Remove John Byrne from Northwind Traders?",
  });
  await expect(dialog).toBeVisible();
  await expectAccessible(browser);
  await dialog.getByRole("button", { name: "Remove member" }).click();

  await expect(
    browser.getByText("John Byrne removed from Northwind Traders"),
  ).toBeVisible();
});

test("Members_InvitationsTab_ShowsTheStatuses", async ({ page: browser }) => {
  await open(browser, {}, `${page}?tab=invitations`);

  const table = browser.getByRole("table", { name: "Invitations" });
  await expect(
    table.getByRole("row", { name: /conor@northwind\.io/ }),
  ).toContainText("Expires soon");
  await expect(
    table.getByRole("row", { name: /padraig@northwind\.io/ }),
  ).toContainText("Expired");
  await expectAccessible(browser);
});

test("Members_Arabic_IsMirrored", async ({ page: browser }) => {
  await open(browser, { language: Language.Ar });

  await expect(
    browser.getByRole("heading", { level: 1, name: "الأعضاء" }),
  ).toBeVisible();
  await expect(browser.locator("html")).toHaveAttribute("dir", "rtl");
  const left = (element: HTMLElement | SVGElement) =>
    element.getBoundingClientRect().left;
  const rail = await browser
    .getByRole("navigation", { name: "القائمة الرئيسية" })
    .evaluate(left);
  expect(rail).toBeGreaterThan(await browser.getByRole("main").evaluate(left));
  await expectAccessible(browser);
});

test("Members_Phone_ShowsCardsAndABottomSheet", async ({ page: browser }) => {
  await browser.setViewportSize({ width: 390, height: 844 });
  await open(browser);

  await expect(browser.getByRole("table", { name: "Members" })).toBeHidden();
  const cards = browser.getByRole("list", { name: "Members" });
  await expect(cards.getByText("John Byrne")).toBeVisible();
  await expectAccessible(browser);
  await cards.getByRole("button", { name: "Actions for John Byrne" }).click();
  await browser
    .getByRole("menuitem", { name: "Remove from organisation" })
    .click();
  const sheet = browser.getByRole("alertdialog");
  await expect(sheet).toBeVisible();
  await expectAccessible(browser);
  expect(
    await sheet.evaluate((element) =>
      Math.round(element.getBoundingClientRect().bottom),
    ),
  ).toBe(844);
});
