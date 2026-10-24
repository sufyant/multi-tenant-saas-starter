import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import {
  customRoles,
  maxCustomRoles,
  mockMembers,
  northwind,
  roles,
} from "./api";
import { expectAccessible } from "./axe";

const base = `/${northwind.slug}/settings/roles`;

const open = async (page: Page, path: string) => {
  await mockMembers(page);
  await setupClerkTestingToken({ page });
  await page.goto(path);
};

test("Roles_Owner_ListsTheRolesWithWhoUsesThem", async ({ page }) => {
  await open(page, base);

  await expect(
    page.getByRole("heading", { level: 1, name: "Roles" }),
  ).toBeVisible();
  await expect(
    page.getByText(`${customRoles.length} of ${maxCustomRoles} custom roles`),
  ).toBeVisible();
  await expect(page.getByRole("row", { name: /Billing lead/ })).toContainText(
    "1 member · 1 pending invitation",
  );
  await expect(page.getByRole("row", { name: /Support/ })).toContainText(
    "Not in use",
  );
  await expectAccessible(page);
});

test("NewRole_Owner_ShowsTheEmptyEditor", async ({ page }) => {
  await open(page, `${base}/new`);

  await expect(
    page.getByRole("heading", { level: 1, name: "New role" }),
  ).toBeVisible();
  await expectAccessible(page);
});

test("Role_CustomRole_OpensItsEditor", async ({ page }) => {
  const [billingLead] = customRoles;
  await open(page, `${base}/${billingLead?.id}`);

  await expect(
    page.getByRole("heading", { level: 1, name: "Billing lead" }),
  ).toBeVisible();
  await expectAccessible(page);
});

test("Role_BuiltInRole_OpensItsPermissions", async ({ page }) => {
  await open(page, `${base}/${roles.admin.id}`);

  await expect(
    page.getByRole("heading", { level: 1, name: "Admin" }),
  ).toBeVisible();
  await expectAccessible(page);
});
