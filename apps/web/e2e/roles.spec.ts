import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import {
  customRoles,
  maxCustomRoles,
  mockMembers,
  northwind,
  pro,
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

test("Roles_CustomRolesOff_ListsThemWithCreateAndEditClosed", async ({
  page,
}) => {
  await mockMembers(page, {
    plan: {
      planName: "Free",
      entitlements: {
        ...pro.entitlements,
        "roles.custom": false,
        "roles.custom.max": 0,
      },
      usage: pro.usage,
    },
  });
  await setupClerkTestingToken({ page });
  await page.goto(base);

  const reason = "Your organisation’s plan doesn’t include custom roles.";
  const create = page.getByRole("button", { name: "Create role" });
  await expect(create).toBeDisabled();
  await expect(create).toHaveAccessibleDescription(reason);
  await expect(page.getByRole("row", { name: /Billing lead/ })).toBeVisible();
  await expect(page.getByText(/0 of 0/)).toHaveCount(0);
  await page
    .getByRole("table", { name: "Roles" })
    .getByRole("button", { name: "Actions for Support" })
    .click();
  await expect(
    page.getByRole("menuitem", { name: /Edit role/ }),
  ).toHaveAttribute("aria-disabled", "true");
  await expect(
    page.getByRole("menuitem", { name: "Delete role" }),
  ).not.toHaveAttribute("aria-disabled");
  await expectAccessible(page);
});
