import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import type { MakeOwnerRequest, UserResponse } from "@repo/api-client";
import { mockMakeOwner, mockPlatform, users } from "./api";
import { expectAccessible } from "./axe";

// Users, and one user with their memberships and Make Owner (the Users, UserDetail and MakeOwner boards).

const open = async (page: Page, path: string) => {
  await setupClerkTestingToken({ page });
  await mockPlatform(page);
  await page.goto(path);
};
/** The row of a user, by the link to their page: a name is not unique, and a user without names has none. */
const row = (page: Page, user: UserResponse) =>
  page
    .getByRole("row")
    .filter({ has: page.locator(`a[href="/users/${user.id}"]`) });

test("Users_List_ShowsTheSystemAdminAndRootTags", async ({ page }) => {
  await open(page, "/users");

  await expect(
    page.getByRole("heading", { level: 1, name: "Users" }),
  ).toBeVisible();
  await expect(row(page, users.root)).toContainText("System admin");
  await expect(row(page, users.root)).toContainText("Root");
  await expect(row(page, users.sam)).toContainText("System admin");
  await expect(row(page, users.sam)).not.toContainText("Root");
  await expect(row(page, users.ayse)).not.toContainText("System admin");
  await expect(row(page, users.mehmet)).toContainText("Suspended");
  await expect(row(page, users.deleted)).toContainText("Deleted user");
  await expectAccessible(page);
});

test("User_WithMemberships_ListsEachOrganisationAndRole", async ({ page }) => {
  await open(page, `/users/${users.ayse.id}`);

  await expect(
    page.getByRole("heading", { level: 1, name: "Ayşe Yılmaz" }),
  ).toBeVisible();
  const memberships = page.getByRole("table", {
    name: "Organisations and roles",
  });
  await expect(
    memberships.getByRole("row", { name: /Northwind Traders/ }),
  ).toContainText("Owner");
  await expect(
    memberships.getByRole("row", { name: /Atlas Logistics/ }),
  ).toContainText("Member");
  await expectAccessible(page);
});

test("MakeOwner_Member_MakesThemAnOwner", async ({ page }) => {
  const bodies: MakeOwnerRequest[] = [];
  await open(page, `/users/${users.ayse.id}`);
  await mockMakeOwner(page, bodies);

  await page
    .getByRole("button", { name: "Actions for Northwind Traders" })
    .click();
  await expect(
    page.getByRole("menuitem", { name: /Make Owner/ }),
  ).toBeDisabled();
  await expect(page.getByText("Already an Owner.")).toBeVisible();
  await page.keyboard.press("Escape");
  // The first menu closes with an animation; the second must not open beside it.
  await expect(page.getByRole("menu")).toBeHidden();
  await page
    .getByRole("button", { name: "Actions for Atlas Logistics" })
    .click();
  await page.getByRole("menuitem", { name: "Make Owner", exact: true }).click();
  const dialog = page.getByRole("alertdialog", {
    name: "Make Ayşe Yılmaz an Owner of Atlas Logistics?",
  });
  await expect(dialog).toBeVisible();
  await expectAccessible(page);
  await dialog.getByRole("button", { name: "Make Owner" }).click();

  await expect(
    page.getByText("Ayşe Yılmaz is an Owner of Atlas Logistics."),
  ).toBeVisible();
  await expect(dialog).toBeHidden();
  expect(bodies).toEqual([{ userId: users.ayse.id }]);
});
