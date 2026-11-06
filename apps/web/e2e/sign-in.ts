import { expect, type Page } from "@playwright/test";
import { savedUser } from "./clerk-users";

// Signs in through our own screens (F41) with Clerk's test mode: an address with +clerk_test takes the code 424242, and no
// email goes out. SignIn shows the code step only after Clerk prepared the code, so the code is never typed too early.
export async function signInWithEmailCode(
  page: Page,
  email: string,
): Promise<void> {
  await page.getByRole("textbox", { name: "Email" }).fill(email);
  await page.getByRole("button", { name: "Continue" }).click();
  await expect(
    page.getByRole("heading", { name: "Check your email" }),
  ).toBeVisible();
  await page.getByRole("textbox", { name: "Code" }).pressSequentially("424242");
  await page.getByRole("button", { name: "Sign in" }).click();
}

/** The address of the setup's temporary user, which never gets an authenticator app. */
export const e2eUserEmail = () => savedUser().email;
