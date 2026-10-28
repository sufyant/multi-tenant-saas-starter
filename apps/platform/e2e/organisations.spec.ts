import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import type { CreateTenantRequest } from "@repo/api-client";
import {
  created,
  mockClose,
  mockCreate,
  mockPlatform,
  organisations,
  plans,
} from "./api";
import { expectAccessible } from "./axe";

// Organisations, the platform's start page, and one organisation in each state (the Main, CreateOrg, OrgDetail, OrgOwnerless,
// OrgClosed, OrgPurged and CloseOrg boards).

const open = async (page: Page, path: string) => {
  await setupClerkTestingToken({ page });
  await mockPlatform(page);
  await page.goto(path);
};
const pageOf = (id: string) => `/organisations/${id}`;
const row = (page: Page, name: string) =>
  page.getByRole("row", { name: new RegExp(name) });

test("Organisations_List_ShowsEachStatus", async ({ page }) => {
  await open(page, "/");

  await expect(
    page.getByRole("heading", { level: 1, name: "Organisations" }),
  ).toBeVisible();
  await expect(row(page, "Northwind Traders")).toContainText("1 Owner");
  await expect(row(page, "Northwind Traders")).toContainText("Active");
  await expect(row(page, "Atlas Logistics")).toContainText("Waiting for Owner");
  await expect(row(page, "Atlas Logistics")).toContainText(
    "owner@atlas.example",
  );
  await expect(row(page, "Shannon Studio")).toContainText("Suspended");
  await expect(row(page, "Kestrel Labs")).toContainText("Closed");
  await expect(row(page, "Kestrel Labs")).toContainText("Deleted in 20 days");
  await expect(row(page, "Liffey Design")).toContainText("Deleted");
  await expectAccessible(page);
});

test("Organisations_SuspendedTab_ListsOnlyTheSuspended", async ({ page }) => {
  await open(page, "/");

  await page.getByRole("tab", { name: "Suspended" }).click();

  await expect(page).toHaveURL(/status=suspended/);
  await expect(row(page, "Shannon Studio")).toBeVisible();
  await expect(row(page, "Northwind Traders")).toBeHidden();
  await expectAccessible(page);
});

test("CreateOrganisation_Valid_OpensTheNewOrganisation", async ({ page }) => {
  const bodies: CreateTenantRequest[] = [];
  await open(page, "/");
  await mockCreate(page, bodies);

  await page.getByRole("button", { name: "Create organisation" }).click();
  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toBeVisible();
  await expectAccessible(page);
  await dialog.getByRole("textbox", { name: "Name" }).fill(created.name);
  await dialog.getByRole("textbox", { name: "Slug" }).fill(created.slug);
  await dialog
    .getByRole("textbox", { name: "Owner’s email" })
    .fill("owner@corrib.example");
  await dialog.getByRole("button", { name: "Create and invite Owner" }).click();

  await expect(page).toHaveURL(pageOf(created.id));
  await expect(
    page.getByRole("heading", { level: 1, name: created.name }),
  ).toBeVisible();
  await expect(page.getByText(`${created.name} created`)).toBeVisible();
  expect(bodies).toEqual([
    {
      name: created.name,
      slug: created.slug,
      ownerEmail: "owner@corrib.example",
      language: "en",
      planId: plans.free.id,
    },
  ]);
  await expectAccessible(page);
});

test("Organisation_Active_ShowsItsOwner", async ({ page }) => {
  await open(page, pageOf(organisations.northwind.id));

  await expect(
    page.getByRole("heading", { level: 1, name: "Northwind Traders" }),
  ).toBeVisible();
  await expect(page.getByRole("main")).toContainText("Active");
  await expect(page.getByText("ayse@northwind.io")).toBeVisible();
  await expect(page.getByRole("tab", { name: "Overview" })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await expectAccessible(page);
});

test("Organisation_WaitingForOwner_ShowsTheInvitation", async ({ page }) => {
  await open(page, pageOf(organisations.atlas.id));

  await expect(page.getByText("Waiting for the Owner.")).toBeVisible();
  await expect(page.getByRole("main")).toContainText(
    "The link to owner@atlas.example works until",
  );
  await expect(
    page.getByRole("button", { name: "Invite Owner" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "open their user page" }),
  ).toHaveAttribute("href", "/users?q=owner%40atlas.example");
  await expectAccessible(page);
});

test("Organisation_Closed_ShowsTheWarningDate", async ({ page }) => {
  await open(page, pageOf(organisations.kestrel.id));

  // The warning is 13 days ahead (api.ts), so it is still to come.
  await expect(page.getByRole("main")).toContainText(
    /Closed on .+\. You can reopen it until .+\. After that it is deleted\. The Owners are warned on .+\./,
  );
  await expect(page.getByRole("button", { name: "Reopen" })).toBeVisible();
  await expectAccessible(page);
});

test("Organisation_Deleted_ShowsWhenAndNoActions", async ({ page }) => {
  await open(page, pageOf(organisations.liffey.id));

  await expect(
    page.getByRole("heading", { level: 1, name: "Liffey Design" }),
  ).toBeVisible();
  await expect(page.getByRole("term")).toHaveText(["Created", "Deleted"]);
  await expect(
    page.getByText("Its members, invitations and data are gone."),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Actions for Liffey Design" }),
  ).toHaveCount(0);
  await expectAccessible(page);
});

test("CloseOrganisation_TypedSlug_ClosesIt", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockPlatform(page);
  await mockClose(page);
  await page.goto(pageOf(organisations.northwind.id));

  await page
    .getByRole("button", { name: "Actions for Northwind Traders" })
    .click();
  await page.getByRole("menuitem", { name: "Close organisation" }).click();
  const dialog = page.getByRole("alertdialog", {
    name: "Close Northwind Traders?",
  });
  const close = dialog.getByRole("button", { name: "Close organisation" });
  await expect(close).toBeDisabled();
  await expectAccessible(page);
  await dialog
    .getByRole("textbox", { name: "Type northwind to confirm" })
    .fill("northwind");
  await close.click();

  await expect(page.getByText("Northwind Traders closed")).toBeVisible();
  await expect(page.getByText(/^You can reopen it until .+\./)).toBeVisible();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("button", { name: "Reopen" })).toBeVisible();
  await expectAccessible(page);
});
