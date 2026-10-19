import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { expectAccessible } from "./axe";

test("SignIn_SignedOutVisitor_IsSentToSignIn", async ({ page }) => {
  await setupClerkTestingToken({ page });

  await page.goto("/");

  await expect(page).toHaveURL(/\/sign-in/);
  await expect(
    page.getByRole("heading", { level: 1, name: "Sign in to the platform" }),
  ).toBeVisible();
  await expect(page.getByRole("banner")).toContainText("Platform");
  await expectAccessible(page);
});

// Each provider button starts Clerk's redirect to the provider. The test stops at the provider's door: it signs in to nothing.
for (const [name, host] of [
  ["Continue with Google", "accounts.google.com"],
  ["Continue with Microsoft", "login.microsoftonline.com"],
] as const) {
  test(`SignIn_${name.replaceAll(" ", "")}_StartsTheRedirectToTheProvider`, async ({
    page,
  }) => {
    await setupClerkTestingToken({ page });
    await page.route(`https://${host}/**`, (route) => route.abort());
    await page.goto("/sign-in");

    const toProvider = page.waitForRequest((request) =>
      request.url().startsWith(`https://${host}/`),
    );
    await page.getByRole("button", { name }).click();

    expect((await toProvider).url()).toContain(host);
  });
}
