import { expect, type Page } from "@playwright/test";

// Signs in through our own screens (F41) with Clerk's test mode: an address with +clerk_test takes the code 424242, and no
// email goes out. SignIn shows the code step only after Clerk prepared the code, so the code is never typed too early.
export async function signInWithEmailCode(
  page: Page,
  email: string,
): Promise<void> {
  await page.getByRole("textbox", { name: "Email" }).fill(email);
  await page.getByRole("button", { name: "Email me a code" }).click();
  await expect(
    page.getByRole("heading", { name: "Check your email" }),
  ).toBeVisible();
  await page.getByRole("textbox", { name: "Code" }).pressSequentially("424242");
  await page.getByRole("button", { name: "Sign in" }).click();
}

/** The address of the development instance's user, which never gets an authenticator app. */
export function e2eUserEmail(): string {
  const email = process.env.E2E_CLERK_USER_EMAIL;
  if (!email?.includes("+clerk_test"))
    throw new Error("Set E2E_CLERK_USER_EMAIL to an address with +clerk_test.");
  return email;
}
