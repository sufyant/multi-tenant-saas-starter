import { randomUUID } from "node:crypto";
import { clerk, setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test as setup } from "@playwright/test";
import { mockApi } from "./api";
import { expectAccessible } from "./axe";
import { createUser } from "./clerk-users";
import {
  leaveTwoStep,
  passTheChallenge,
  saveUser,
  signInWithEmailCode,
  totpCode,
} from "./two-step";

// Decision 5 of step 21b: the platform's tests use their own temporary user. It enrols an authenticator app through our screens,
// saves its backup codes, signs out, signs in again and passes the challenge. That session serves the signed-in tests. The
// teardown deletes the user, also when a test failed.
setup(
  "enrol a temporary user and sign in with the second factor",
  async ({ page }) => {
    setup.setTimeout(180_000);
    const email = `e2e-platform-${randomUUID().slice(0, 8)}+clerk_test@example.com`;
    const id = await createUser(email);
    saveUser({ id, email });
    await setupClerkTestingToken({ page });
    await mockApi(page);

    await page.goto("/sign-in");
    await signInWithEmailCode(page, email);
    await expect(
      page.getByRole("heading", { name: "Set up your authenticator app" }),
    ).toBeVisible({ timeout: 30_000 });
    await expectAccessible(page);
    const key = page.getByRole("textbox", {
      name: "Cannot scan? Type this key instead:",
    });
    await expect(key).not.toHaveValue("");
    const secret = (await key.inputValue()).replaceAll(" ", "");
    await page
      .getByRole("textbox", { name: "Enter the 6-digit code the app shows" })
      .pressSequentially(await totpCode(secret));
    await page
      .getByRole("button", { name: "Turn on two-step sign-in" })
      .click();

    const codes = page
      .getByRole("list", { name: "Backup codes" })
      .getByRole("listitem");
    // An error under the code names Clerk's answer in the report, where a missing list would say nothing. Only the main
    // region counts: Next.js keeps an empty alert of its own, the route announcer.
    const errors = page.getByRole("main").getByRole("alert");
    await expect(codes.or(errors)).not.toHaveCount(0);
    expect(await errors.allInnerTexts()).toEqual([]);
    await expect(codes).toHaveCount(10);
    await expectAccessible(page);
    saveUser({ id, email, secret, backupCodes: await codes.allInnerTexts() });
    await page.getByRole("checkbox").check();
    await page
      .getByRole("button", { name: "Continue to the platform" })
      .click();
    await leaveTwoStep(page, secret);

    await clerk.signOut({ page });
    await page.goto("/sign-in");
    await signInWithEmailCode(page, email);
    await passTheChallenge(page, secret);
    await expect(page).not.toHaveURL(/sign-in|two-step/, { timeout: 30_000 });
    await page.context().storageState({ path: "playwright/.clerk/user.json" });
  },
);
