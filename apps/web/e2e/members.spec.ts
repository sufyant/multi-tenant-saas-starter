import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
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
  await expect(
    browser.getByText("khalid@northwind.io has 7 days to join."),
  ).toBeVisible();
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
