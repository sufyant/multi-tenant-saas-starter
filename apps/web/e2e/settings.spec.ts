import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { MyTenantResponseStatus } from "@repo/api-client";
import { BuiltInRole } from "../src/shared/roles";
import { mockAuditLog, mockMembers, mockTenantStatus, northwind } from "./api";
import { expectAccessible } from "./axe";

const settings = `/${northwind.slug}/settings`;

const open = async (page: Page, path: string) => {
  await setupClerkTestingToken({ page });
  await page.goto(path);
};

test("General_Owner_ShowsTheOrganisationReadOnly", async ({ page }) => {
  await mockMembers(page);
  await open(page, `${settings}/general`);

  await expect(
    page.getByRole("heading", { level: 1, name: "General" }),
  ).toBeVisible();
  await expect(page.getByText("Northwind Traders").last()).toBeVisible();
  await expect(page.getByText("Active")).toBeVisible();
  await expect(page.getByRole("textbox")).toHaveCount(0);
  await expectAccessible(page);
});

test("Rail_Settings_OpensGeneral", async ({ page }) => {
  await mockMembers(page, { role: BuiltInRole.Member });
  await open(page, `/${northwind.slug}`);

  await page
    .getByRole("navigation", { name: "Main" })
    .getByRole("link", { name: "Settings" })
    .click();

  await expect(page).toHaveURL(`${settings}/general`);
  await expect(
    page.getByRole("heading", { level: 1, name: "General" }),
  ).toBeVisible();
  await expectAccessible(page);
});

test("Activity_TwoPages_ShowsOlderActivityThenNoButton", async ({ page }) => {
  await mockAuditLog(page);
  await open(page, `${settings}/activity`);

  await expect(
    page.getByText("Khalid Al-Harbi joined the organisation."),
  ).toBeVisible();
  await expect(
    page.getByText("The platform reactivated this organisation."),
  ).toBeVisible();
  await expectAccessible(page);
  await page.getByRole("button", { name: "Show older activity" }).click();

  await expect(
    page.getByText("Ayşe Yılmaz created this organisation."),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Show older activity" }),
  ).toHaveCount(0);
  await expectAccessible(page);
});

test("Activity_Member_SaysPermissionDenied", async ({ page }) => {
  await mockAuditLog(page, { role: BuiltInRole.Member });
  await open(page, `${settings}/activity`);

  await expect(
    page.getByRole("heading", { name: "You can’t open Activity" }),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: "Activity" })).toHaveCount(0);
  await expectAccessible(page);
});

test("Organisation_Suspended_ShowsItsPageWithNoReason", async ({ page }) => {
  await mockTenantStatus(page, MyTenantResponseStatus.Suspended);
  await open(page, `/${northwind.slug}`);

  await expect(
    page.getByRole("heading", { name: "Northwind Traders is suspended" }),
  ).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Main" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Sign out" })).toBeVisible();
  await expectAccessible(page);
});

test("Organisation_Closed_ShowsItsPage", async ({ page }) => {
  await mockTenantStatus(page, MyTenantResponseStatus.Closed, {
    role: BuiltInRole.Member,
  });
  await open(page, `${settings}/members`);

  await expect(
    page.getByRole("heading", { name: "Northwind Traders is closed" }),
  ).toBeVisible();
  await expectAccessible(page);
});
