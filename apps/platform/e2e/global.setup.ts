import { clerkSetup, setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test as setup } from "@playwright/test";

// Clerk's test mode: an address with +clerk_test signs in with the code 424242, and no email goes out. The password strategy is
// off (.clerk/config.json), so the test uses the email code. E2E_CLERK_USER_EMAIL names the user, for example
// e2e+clerk_test@example.com; the API must know that user (GetMe), or the page tests mock the API (e2e/api.ts).
setup.describe.configure({ mode: "serial" });

setup("clerk", async () => {
  await clerkSetup();
});

setup("sign in through our sign-in page", async ({ page }) => {
  const email = process.env.E2E_CLERK_USER_EMAIL;
  if (!email?.includes("+clerk_test"))
    throw new Error("Set E2E_CLERK_USER_EMAIL to an address with +clerk_test.");
  await setupClerkTestingToken({ page });
  await page.goto("/sign-in");
  await page.getByRole("textbox", { name: /email/i }).fill(email);
  await page.getByRole("button", { name: /continue/i }).click();
  await page.getByRole("textbox", { name: /code/i }).first().waitFor();
  await page.keyboard.type("424242");
  await expect(page).not.toHaveURL(/sign-in/, { timeout: 30_000 });
  await page.context().storageState({ path: "playwright/.clerk/user.json" });
});
