import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { mockPlatform } from "./api";
import { expectAccessible } from "./axe";
import { frameTerms, mockFrameAdd, mockFrameTerms } from "./terms-frames";

// Settings › Terms of use (step 38): the list, a version with its people, New version and Edit version, each checked by axe
// (F37). The API is mocked with the versions of the frames (`terms-frames.ts`).

const open = async (
  page: Page,
  path: string,
  mock: (page: Page) => Promise<void> = mockFrameTerms(),
) => {
  await setupClerkTestingToken({ page });
  await mockPlatform(page);
  await mock(page);
  await page.goto(path);
};

test.afterEach(({ page }) => page.unrouteAll({ behavior: "ignoreErrors" }));

test("TermsList_Opens_ShowsTheVersionsAndPassesAxe", async ({ page }) => {
  await open(page, "/settings/terms");

  await expect(
    page
      .getByRole("table", { name: "Terms of use" })
      .getByText("18 of 67 accepted"),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Add version" }),
  ).toBeDisabled();
  await expectAccessible(page);
});

test("TermsVersion_Opens_ShowsItsPeopleAndPassesAxe", async ({ page }) => {
  await open(page, `/settings/terms/${frameTerms.scheduled.id}`);

  await expect(
    page.getByRole("table", { name: "Accepted" }).getByText("Ayşe Demir"),
  ).toBeVisible();
  await expectAccessible(page);
  await page.getByRole("tab", { name: /Not yet/ }).click();
  await expect(
    page.getByRole("table", { name: "Not yet" }).getByText("Aiko Tanaka"),
  ).toBeVisible();
  await expect(page).toHaveURL(/people=not_yet/);
});

test("NewTermsVersion_Opens_PassesAxe", async ({ page }) => {
  await open(page, "/settings/terms/new", mockFrameAdd);

  await expect(
    page.getByRole("checkbox", { name: "Send an email" }),
  ).toBeChecked();
  await expectAccessible(page);
});

test("EditTermsVersion_Opens_LocksTheAcceptedFieldsAndPassesAxe", async ({
  page,
}) => {
  await open(page, `/settings/terms/${frameTerms.scheduled.id}/edit`);

  await expect(page.getByRole("textbox", { name: "Link" })).toBeDisabled();
  await expect(
    page.getByRole("checkbox", { name: "Send an email" }),
  ).toBeDisabled();
  await expectAccessible(page);
});
