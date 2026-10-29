import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { FeatureKey, LimitKey } from "@repo/api-client";
import {
  mockOverride,
  mockPlanEdit,
  mockPlatform,
  organisations,
  plans,
} from "./api";
import { expectAccessible } from "./axe";

// Settings › Plans, the plan editor with its impact preview, and an organisation's Plan section with an override (the Plans
// boards).

const open = async (page: Page, path: string) => {
  await setupClerkTestingToken({ page });
  await mockPlatform(page);
  await page.goto(path);
};

test("Plans_List_ShowsEachPlanAndTheDefault", async ({ page }) => {
  await open(page, "/settings/plans");

  await expect(
    page.getByRole("heading", { level: 1, name: "Plans" }),
  ).toBeVisible();
  await expect(page.getByRole("row", { name: /Free/ })).toContainText(
    "Default",
  );
  await expect(page.getByRole("row", { name: /Pro/ })).toContainText("6");
  await expect(page.getByRole("link", { name: "New plan" })).toBeVisible();
  await expectAccessible(page);
});

test("PlanEditor_Pro_ShowsEveryKeyByArea", async ({ page }) => {
  await open(page, `/settings/plans/${plans.pro.id}`);

  await expect(
    page.getByRole("heading", { level: 1, name: "Pro" }),
  ).toBeVisible();
  await expect(
    page.getByRole("switch", { name: "Custom roles" }),
  ).toBeChecked();
  await expect(
    page
      .getByRole("group", { name: "Custom roles limit" })
      .getByRole("spinbutton"),
  ).toHaveValue("20");
  await expect(
    page.getByRole("group", { name: "Members limit" }).getByRole("checkbox", {
      name: "Unlimited",
    }),
  ).toBeChecked();
  await expectAccessible(page);
});

test("PlanEditor_DefaultPlan_KeepsItsMark", async ({ page }) => {
  await open(page, `/settings/plans/${plans.free.id}`);

  const mark = page.getByRole("checkbox", { name: "Default plan" });
  await expect(mark).toBeChecked();
  await expect(mark).toBeDisabled();
  await expectAccessible(page);
});

test("PlanEditor_LowerALimit_ShowsTheImpactAndSavesAnyway", async ({
  page,
}) => {
  const bodies: unknown[] = [];
  await open(page, `/settings/plans/${plans.pro.id}`);
  await mockPlanEdit(page, bodies);

  await page
    .getByRole("group", { name: "Custom roles limit" })
    .getByRole("spinbutton")
    .fill("10");
  await page.getByRole("button", { name: "Save changes" }).click();

  const dialog = page.getByRole("alertdialog", {
    name: "Save changes to Pro?",
  });
  await expect(dialog).toBeVisible();
  await expect(
    dialog.getByRole("link", { name: /Northwind Traders/ }),
  ).toHaveAttribute("target", "_blank");
  await expect(dialog).toContainText("Over by 4");
  await expectAccessible(page);
  await dialog.getByRole("button", { name: "Save anyway" }).click();

  await expect(page).toHaveURL("/settings/plans");
  expect(bodies).toEqual([
    {
      name: "Pro",
      isDefault: false,
      entitlements: {
        [FeatureKey.RolesCustom]: { enabled: true },
        [LimitKey.RolesCustomMax]: { maximum: 10 },
        [LimitKey.MembersMax]: { unlimited: true },
      },
    },
  ]);
});

test("Organisation_PlanSection_ShowsThePlanAndTheOverride", async ({
  page,
}) => {
  await open(page, `/organisations/${organisations.northwind.id}`);

  await expect(
    page.getByRole("heading", { level: 2, name: "Plan" }),
  ).toBeVisible();
  const table = page.getByRole("table", { name: "Plan" });
  await expect(table).toContainText("Custom roles limit");
  await expect(table).toContainText("Set by Sam Kelly");
  await expect(page.getByRole("button", { name: "Change plan" })).toBeVisible();
  await expectAccessible(page);
});

test("Organisation_AddOverride_SavesIt", async ({ page }) => {
  const bodies: unknown[] = [];
  const { northwind } = organisations;
  await open(page, `/organisations/${northwind.id}`);
  await mockOverride(page, northwind.id, bodies);

  await page.getByRole("button", { name: "Actions for Members limit" }).click();
  await page.getByRole("menuitem", { name: "Add override" }).click();
  const dialog = page.getByRole("alertdialog", {
    name: "Add an override for Northwind Traders",
  });
  await expect(dialog).toBeVisible();
  await expectAccessible(page);
  await dialog.getByRole("checkbox", { name: "Unlimited" }).click();
  await dialog.getByRole("spinbutton", { name: "Value" }).fill("40");
  await dialog.getByRole("textbox", { name: "Reason" }).fill("Trial");
  await dialog.getByRole("button", { name: "Save override" }).click();

  await expect(dialog).toBeHidden();
  expect(bodies).toEqual([
    { value: { maximum: 40 }, expiresAt: null, reason: "Trial" },
  ]);
});
