import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test as setup } from "@playwright/test";

// Clerk's test mode: an address with +clerk_test signs in with the code 424242, and no email goes out. The password strategy is
// off (.clerk/config.json), so the test uses the email code. E2E_CLERK_USER_EMAIL names a user of the development instance.
setup("sign in through our sign-in page", async ({ page }) => {
  const email = process.env.E2E_CLERK_USER_EMAIL;
  if (!email?.includes("+clerk_test"))
    throw new Error("Set E2E_CLERK_USER_EMAIL to an address with +clerk_test.");
  await setupClerkTestingToken({ page });
  await page.goto("/sign-in");
  await page.getByRole("textbox", { name: /email/i }).fill(email);
  // Clerk prepares the email code after it shows the code field; a code typed before that is refused.
  const prepared = page.waitForResponse(
    (response) =>
      response.url().includes("prepare_first_factor") && response.ok(),
  );
  await page.getByRole("button", { name: /continue/i }).click();
  await prepared;
  await page
    .getByRole("textbox", { name: /verification code/i })
    .pressSequentially("424242");
  await expect(page).not.toHaveURL(/sign-in/, { timeout: 30_000 });
  await page.context().storageState({ path: "playwright/.clerk/user.json" });
});
