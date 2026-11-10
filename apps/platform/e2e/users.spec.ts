import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import type { MakeOwnerRequest, UserResponse } from "@repo/api-client";
import { mockMakeOwner, mockPlatform, users } from "./api";
import { expectAccessible } from "./axe";

// Users, and one user with their organisations and Make Owner (5.01, 5.05, 5.12).

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

test("Users_List_ShowsTheSystemAdminLabels", async ({ page }) => {
  await open(page, "/users");

  await expect(
    page.getByRole("heading", { level: 1, name: "Users" }),
  ).toBeVisible();
  await expect(row(page, users.root)).toContainText("System admin");
  await expect(row(page, users.root)).not.toContainText("Root");
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
  const memberships = page.getByRole("list", { name: "Organisations" });
  await expect(
    memberships.getByRole("listitem").filter({ hasText: "Northwind Traders" }),
  ).toContainText("Owner");
  await expect(
    memberships.getByRole("listitem").filter({ hasText: "Atlas Logistics" }),
  ).toContainText("Member");
  await expectAccessible(page);
});

test("MakeOwner_Member_MakesThemAnOwner", async ({ page }) => {
  const bodies: MakeOwnerRequest[] = [];
  await open(page, `/users/${users.ayse.id}`);
  await mockMakeOwner(page, bodies);

  // An Owner is offered no Make Owner there (5.05).
  await expect(
    page.getByRole("button", { name: "Make Owner of Northwind Traders" }),
  ).toHaveCount(0);
  await page
    .getByRole("button", { name: "Make Owner of Atlas Logistics" })
    .click();
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
