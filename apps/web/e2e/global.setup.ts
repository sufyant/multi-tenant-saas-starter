import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test as setup } from "@playwright/test";
import { e2eUserEmail, signInWithEmailCode } from "./sign-in";

// The password strategy is off (.clerk/config.json), so the setup signs in with the email code, through our sign-in page. The
// session it keeps serves every signed-in test.
setup("sign in through our sign-in page", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.goto("/sign-in");

  await signInWithEmailCode(page, e2eUserEmail());

  await expect(page).not.toHaveURL(/sign-in/, { timeout: 30_000 });
  await page.context().storageState({ path: "playwright/.clerk/user.json" });
});
