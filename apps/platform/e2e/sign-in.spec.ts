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
  await expect(page.getByText("Platform", { exact: true })).toBeVisible();
  await expectAccessible(page);
});

// Each provider button starts Clerk's redirect to the provider. The test stops at the provider's door: it signs in to nothing.
for (const [provider, name, host] of [
  ["Google", "Continue with Google", "accounts.google.com"],
  ["Microsoft", "Continue with Microsoft", "login.microsoftonline.com"],
] as const) {
  test(`SignIn_${provider}_StartsTheRedirectToTheProvider`, async ({
    page,
  }) => {
    await setupClerkTestingToken({ page });
    await page.route(`https://${host}/**`, (route) => route.abort());
    await page.goto("/sign-in");
    await expectAccessible(page);

    const toProvider = page.waitForRequest((request) =>
      request.url().startsWith(`https://${host}/`),
    );
    await page.getByRole("button", { name }).click();

    expect((await toProvider).url()).toContain(host);
  });
}

// The Auth footer: the language and the privacy policy on every screen before sign-in.
test("SignIn_Footer_ShowsTheLanguageAndPrivacy", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.goto("/sign-in");

  const footer = page.getByRole("navigation", { name: "Language and legal" });
  await expect(footer.getByRole("combobox")).toBeVisible();
  await expect(footer.getByRole("link", { name: "Privacy" })).toHaveAttribute(
    "href",
    /^https:\/\//,
  );
});

// The Session ended screen: sign in again, then back to the same page.
test("SignIn_SessionEnded_SaysSo", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.goto(
    `/sign-in?reason=session-ended&redirect_url=${encodeURIComponent("/users")}`,
  );

  await expect(page.getByText("Your session ended")).toBeVisible();
  await expectAccessible(page);
});

// Signed out, a page that no route has may need a session: "Page not found" offers sign-in, in the frame before sign-in.
test("NotFound_SignedOut_OffersSignIn", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.goto("/sign-in/missing");

  await expect(
    page.getByRole("heading", { name: "Page not found" }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Sign in" })).toHaveAttribute(
    "href",
    "/sign-in",
  );
  await expect(page.getByRole("link", { name: "Privacy" })).toBeVisible();
  await expectAccessible(page);
});

// The environment strip sits above every page outside production, sign-in too. The tests run as APP_ENVIRONMENT=local.
test("SignIn_LocalEnvironment_ShowsTheStripAndTheTabMark", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.goto("/sign-in");

  await expect(
    page.locator("[data-slot=environment-strip]").getByText("Local"),
  ).toBeVisible();
  await expect(page).toHaveTitle(/^\[Local\] /);
});
