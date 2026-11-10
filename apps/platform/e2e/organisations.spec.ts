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

// Organisations, the platform's start page, and one organisation in each state (4.01 to 4.26).

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
  await expect(row(page, "Northwind Traders")).toContainText("6 members");
  await expect(row(page, "Northwind Traders")).toContainText("Active");
  await expect(row(page, "Atlas Logistics")).toContainText("1 member");
  await expect(row(page, "Shannon Studio")).toContainText("Suspended");
  await expect(row(page, "Kestrel Labs")).toContainText("Closed");
  // All holds every status but deleted; the Deleted tab holds Liffey (4.01).
  await expect(page.getByRole("tab", { name: "All 4" })).toBeVisible();
  await expect(row(page, "Liffey Design")).toBeHidden();
  await page.getByRole("tab", { name: "Deleted 1" }).click();
  await expect(row(page, "Liffey Design")).toContainText("Deleted");
  await expectAccessible(page);
});

test("Organisations_SuspendedTab_ListsOnlyTheSuspended", async ({ page }) => {
  await open(page, "/");

  await page.getByRole("tab", { name: "Suspended 1" }).click();

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
  await dialog.getByRole("textbox", { name: "Address" }).fill(created.slug);
  await dialog
    .getByRole("textbox", { name: "Owner’s email" })
    .fill("owner@corrib.example");
  await dialog.getByRole("button", { name: "Create organisation" }).click();

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

  await expect(page.getByText("Waiting for the Owner")).toBeVisible();
  await expect(page.getByRole("main")).toContainText(
    "owner@atlas.example hasn’t accepted yet. The invitation works until",
  );
  await expect(
    page.getByRole("button", { name: "Invite Owner again" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "open their user page" }),
  ).toHaveAttribute("href", "/users?q=owner%40atlas.example");
  await expectAccessible(page);
});

test("Organisation_Closed_ShowsUntilWhenItCanBeReopened", async ({ page }) => {
  await open(page, pageOf(organisations.kestrel.id));

  await expect(page.getByRole("main")).toContainText(
    /Closed on .+It can be reopened until .+\. Then it is deleted with all its data\./,
  );
  await expect(page.getByRole("button", { name: "Reopen" })).toBeVisible();
  await expectAccessible(page);
});

test("Organisation_Deleted_ShowsWhenAndNoActions", async ({ page }) => {
  await open(page, pageOf(organisations.liffey.id));

  await expect(
    page.getByRole("heading", { level: 1, name: "Liffey Design" }),
  ).toBeVisible();
  await expect(page.getByText(/^Deleted on /)).toBeVisible();
  await expect(
    page.getByText(
      "Its data is gone. Its name and address stay reserved, and the Activity log keeps its entries.",
    ),
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
