import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { expect, type Page } from "@playwright/test";
import { Secret, TOTP } from "otpauth";

// What the setup keeps for the other tests and the teardown, beside the session in playwright/.clerk (ignored by git): the
// temporary user, the authenticator's secret and the backup codes. Nothing of it goes into a log or a report.
export type PlatformUser = {
  id: string;
  email: string;
  secret?: string;
  backupCodes?: string[];
};

export const userFile = "playwright/.clerk/platform-user.json";

export const saveUser = (user: PlatformUser) =>
  writeFileSync(userFile, JSON.stringify(user));

export const readUser = (): PlatformUser | undefined =>
  existsSync(userFile)
    ? (JSON.parse(readFileSync(userFile, "utf8")) as PlatformUser)
    : undefined;

/** The setup's user and its first backup code; a test that needs them fails at once without them. */
export function enrolledUser(): { email: string; backupCode: string } {
  const user = readUser();
  const backupCode = user?.backupCodes?.[0];
  if (!user || !backupCode)
    throw new Error("The setup did not enrol the temporary user.");
  return { email: user.email, backupCode };
}

const period = 30;
let lastStep = -1;

/**
 * The authenticator app's code. Clerk may refuse a code a second time in the same 30 seconds, so after a code was used the next
 * one waits for the next window.
 */
export async function totpCode(secret: string): Promise<string> {
  const step = () => Math.floor(Date.now() / 1000 / period);
  while (step() <= lastStep)
    await new Promise((resolve) => setTimeout(resolve, 1000));
  lastStep = step();
  return new TOTP({ secret: Secret.fromBase32(secret), period }).generate();
}

/** Signs in through our own screens with Clerk's test code 424242. */
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

/** Types the authenticator's code into the second-factor step, of sign-in or of /two-step. */
export async function passTheChallenge(
  page: Page,
  secret: string,
): Promise<void> {
  await expect(
    page.getByRole("heading", { name: "Enter your authenticator code" }),
  ).toBeVisible();
  await page
    .getByRole("textbox", { name: "Code" })
    .pressSequentially(await totpCode(secret));
  await page.getByRole("button", { name: "Continue" }).click();
}

/**
 * After the backup codes: the platform, or the challenge when Clerk did not mark the session with the second factor (gate C of
 * step 21b). Either way the page ends outside /two-step.
 */
export async function leaveTwoStep(page: Page, secret: string): Promise<void> {
  const challenge = page.getByRole("heading", {
    name: "Enter your authenticator code",
  });
  await Promise.race([
    challenge.waitFor({ timeout: 30_000 }),
    page.waitForURL((url) => !url.pathname.startsWith("/two-step"), {
      timeout: 30_000,
    }),
  ]);
  if (await challenge.isVisible()) await passTheChallenge(page, secret);
  await expect(page).not.toHaveURL(/two-step/, { timeout: 30_000 });
}
