import { randomUUID } from "node:crypto";
import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test as setup } from "@playwright/test";
import {
  createUser,
  deleteStaleUsers,
  newEmail,
  saveUser,
} from "./clerk-users";
import { signInWithEmailCode } from "./sign-in";

// The web's tests use their own temporary user, as the platform's do (the maintainer's decision of 2026-11-06), so no run
// leaves a session for the next. The password strategy is off (.clerk/config.json), so the setup signs in with the email code,
// through our sign-in page. The session it keeps serves every signed-in test; the teardown deletes the user, also when a test
// failed.
setup(
  "make a temporary user and sign in through our sign-in page",
  async ({ page }) => {
    // The count of stale users is no secret; the CI log shows it.
    console.log(`Deleted ${await deleteStaleUsers()} stale e2e-web users.`);
    const email = newEmail(randomUUID().slice(0, 8));
    saveUser({ id: await createUser(email), email });
    await setupClerkTestingToken({ page });
    await page.goto("/sign-in");

    await signInWithEmailCode(page, email);

    await expect(page).not.toHaveURL(/sign-in/, { timeout: 30_000 });
    await page.context().storageState({ path: "playwright/.clerk/user.json" });
  },
);
