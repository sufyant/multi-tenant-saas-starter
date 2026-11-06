import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { Language } from "@repo/api-client";
import { mockApi } from "./api";
import { expectAccessible } from "./axe";

// Account and security (F62): the API's parts are mocked; the identity provider's parts read the real development instance, where
// the setup's temporary user has this one session and an authenticator.
test("Account_English_SaysTwoStepIsOnAndShowsThisDevice", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.En);

  await page.goto("/settings/account");

  await expect(
    page.getByRole("heading", { level: 1, name: "Account and security" }),
  ).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Two-step verification" }),
  ).toContainText("On");
  await expect(page.getByText("This device")).toBeVisible();
  await expectAccessible(page);
});
