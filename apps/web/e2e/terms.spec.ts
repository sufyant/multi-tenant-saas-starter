import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { MyTermsResponseAction } from "@repo/api-client";
import { decemberTerms, mockApi, mockMyTerms, northwind } from "./api";
import { expectAccessible } from "./axe";

// The terms after sign-in (the API's terms flow, section 4): the gate of a version that requires acceptance, and Home's notice of
// one that does not. The API is mocked; the BFF has its own tests.
test.beforeEach(async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page);
});

test("TermsGate_Acceptance_AcceptsAndOpensThePage", async ({ page }) => {
  await mockMyTerms(page, {
    action: MyTermsResponseAction.Acceptance,
    version: decemberTerms,
  });
  await page.route("**/api/v1/me/terms/versions/*/accept", (route) =>
    route.fulfill({
      json: { action: MyTermsResponseAction.Nothing, version: null },
    }),
  );
  await page.goto(`/${northwind.slug}`);

  await expect(
    page.getByRole("heading", { level: 1, name: "Terms of use updated" }),
  ).toBeVisible();
  await expectAccessible(page);
  await page
    .getByRole("checkbox", {
      name: "I have read and agree to the Terms of use",
    })
    .click();
  await page.getByRole("button", { name: "Accept" }).click();

  await expect(
    page.getByRole("heading", { level: 1, name: /^Welcome/ }),
  ).toBeVisible();
});

test("TermsNotice_Notice_ShowsOnHomeAndCloses", async ({ page }) => {
  await mockMyTerms(page, {
    action: MyTermsResponseAction.Notice,
    version: { ...decemberTerms, requiresAcceptance: false },
  });
  await page.route("**/api/v1/me/terms/versions/*/dismiss", (route) =>
    route.fulfill({
      json: { action: MyTermsResponseAction.Nothing, version: null },
    }),
  );
  await page.goto(`/${northwind.slug}`);

  const notice = page
    .getByRole("status")
    .filter({ hasText: "Terms of use updated" });
  await expect(notice).toContainText("The new terms apply from 1 Dec 2026.");
  await expectAccessible(page);
  await notice.getByRole("button", { name: "Close" }).click();

  await expect(notice).toHaveCount(0);
});
