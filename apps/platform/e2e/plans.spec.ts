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
  await open(page, "/plans");

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
  await open(page, `/plans/${plans.pro.id}`);

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
    page.getByRole("group", { name: "Seats" }).getByRole("checkbox", {
      name: "Unlimited",
    }),
  ).toBeChecked();
  await expectAccessible(page);
});

test("PlanEditor_DefaultPlan_KeepsItsMark", async ({ page }) => {
  await open(page, `/plans/${plans.free.id}`);

  const mark = page.getByRole("checkbox", { name: "Default plan" });
  await expect(mark).toBeChecked();
  await expect(mark).toBeDisabled();
  await expectAccessible(page);
});

test("PlanEditor_LowerALimit_ShowsTheImpactAndSavesAnyway", async ({
  page,
}) => {
  const bodies: unknown[] = [];
  await open(page, `/plans/${plans.pro.id}`);
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

  // The person stays on the plan, in its saved state (6.08).
  await expect(page.getByText("Pro saved")).toBeVisible();
  await expect(page).toHaveURL(`/plans/${plans.pro.id}`);
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
  await open(page, `/organisations/${organisations.northwind.id}?tab=plan`);

  const limit = page.getByRole("group", { name: "Custom roles limit" });
  await expect(limit).toContainText("4 of 25");
  await expect(limit).toContainText("by Sam Kelly");
  await expect(page.getByRole("button", { name: "Change plan" })).toBeVisible();
  await expectAccessible(page);
});

test("Organisation_AddOverride_SavesIt", async ({ page }) => {
  const bodies: unknown[] = [];
  const { northwind } = organisations;
  await open(page, `/organisations/${northwind.id}?tab=plan`);
  await mockOverride(page, northwind.id, bodies);

  await page.getByRole("button", { name: "Add override" }).click();
  const dialog = page.getByRole("alertdialog", { name: "Add an override" });
  await expect(dialog).toBeVisible();
  await dialog.getByRole("combobox", { name: "Feature or limit" }).click();
  await page.getByRole("option", { name: "Seats" }).click();
  await expectAccessible(page);
  await dialog.getByRole("checkbox", { name: "Unlimited" }).click();
  await dialog.getByRole("spinbutton", { name: "Value" }).fill("40");
  await dialog.getByRole("textbox", { name: "Reason" }).fill("Trial");
  await dialog.getByRole("button", { name: "Add override" }).click();

  await expect(dialog).toBeHidden();
  expect(bodies).toEqual([
    { value: { maximum: 40 }, expiresAt: null, reason: "Trial" },
  ]);
});
