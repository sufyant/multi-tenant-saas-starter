import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { mockApi } from "./api";
import { expectAccessible } from "./axe";
import { enrolledUser, signInWithEmailCode } from "./two-step";

// A new browser with no session: the sign-in asks for the second factor, and a backup code passes it once.
test("TwoStep_BackupCode_SignsInOnceAndNeverAgain", async ({ browser }) => {
  test.setTimeout(120_000);
  const { email, backupCode } = enrolledUser();
  // axe needs a page of a context of its own.
  const first = await (await browser.newContext()).newPage();
  await setupClerkTestingToken({ page: first });
  await mockApi(first);
  await first.goto("/sign-in");
  await signInWithEmailCode(first, email);

  await first.getByRole("button", { name: "Use a backup code" }).click();
  await expectAccessible(first);
  await first.getByRole("textbox", { name: "Backup code" }).fill(backupCode);
  await first.getByRole("button", { name: "Continue" }).click();
  await expect(first).not.toHaveURL(/sign-in|two-step/, { timeout: 30_000 });

  const second = await (await browser.newContext()).newPage();
  await setupClerkTestingToken({ page: second });
  await second.goto("/sign-in");
  await signInWithEmailCode(second, email);
  await second.getByRole("button", { name: "Use a backup code" }).click();
  await second.getByRole("textbox", { name: "Backup code" }).fill(backupCode);
  await second.getByRole("button", { name: "Continue" }).click();

  await expect(
    second.getByText(
      "That backup code does not work. Check it, or use another one.",
    ),
  ).toBeVisible();
});
