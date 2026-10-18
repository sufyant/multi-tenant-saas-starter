import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { expectAccessible } from "./axe";

test("SignIn_SignedOutVisitor_IsSentToSignIn", async ({ page }) => {
  await setupClerkTestingToken({ page });

  await page.goto("/");

  await expect(page).toHaveURL(/\/sign-in/);
  await expect(page.getByRole("textbox", { name: /email/i })).toBeVisible();
  await expectAccessible(page);
});
