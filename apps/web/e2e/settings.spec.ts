import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { BuiltInRole, MyTenantResponseStatus } from "@repo/api-client";
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

test("MainMenu_Settings_OpensProfile", async ({ page }) => {
  await mockMembers(page, { role: BuiltInRole.Member });
  await open(page, `/${northwind.slug}`);

  await page
    .getByRole("navigation", { name: "Main menu" })
    .getByRole("link", { name: "Settings" })
    .click();

  await expect(page).toHaveURL(`${settings}/profile`);
  await expect(
    page.getByRole("heading", { level: 1, name: "Profile" }),
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
    page.getByRole("heading", { name: "You can’t open this page" }),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: "Activity log" })).toHaveCount(0);
  await expectAccessible(page);
});

test("Organisation_Suspended_ShowsItsPageWithNoReason", async ({ page }) => {
  await mockTenantStatus(page, MyTenantResponseStatus.Suspended);
  await open(page, `/${northwind.slug}`);

  await expect(
    page.getByRole("heading", { name: "Northwind Traders is suspended" }),
  ).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Main menu" })).toHaveCount(
    0,
  );
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

// Frame 1.18 and the roadmap: an organisation that opened is "last used" on the start page, from a cookie that holds only its
// slug, which no script of the page can read.
test("ChooseOrganisation_AfterAnOrganisationOpened_SaysLastUsedFromAnHttpOnlyCookie", async ({
  page,
  context,
}) => {
  await mockMembers(page);
  const atlas = {
    ...northwind,
    id: "0198f0a2-0000-7000-8000-0000000000b4",
    slug: "atlas-logistics",
    name: "Atlas Logistics",
  };
  await page.route("**/api/v1/me/tenants*", (route) =>
    route.fulfill({
      json: { items: [atlas, northwind], page: 1, pageSize: 100, total: 2 },
    }),
  );
  await open(page, `/${northwind.slug}`);
  await expect(
    page.getByRole("heading", { level: 1, name: /Welcome/ }),
  ).toBeVisible();
  await expect
    .poll(async () =>
      (await context.cookies()).find(
        (cookie) => cookie.name === "last-organisation",
      ),
    )
    .toMatchObject({
      value: northwind.slug,
      httpOnly: true,
      secure: true,
      sameSite: "Lax",
    });

  await page.goto("/");

  const links = page.getByRole("main").getByRole("link");
  await expect(links.first()).toHaveText(/Northwind Traders.*last used/);
  await expect(links.nth(1)).toHaveText(/Atlas Logistics/);
  await expectAccessible(page);
});
