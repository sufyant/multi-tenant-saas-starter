import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { expectAccessible } from "./axe";
import { e2eUserEmail } from "./sign-in";

test("SignIn_SignedOutVisitor_IsSentToSignIn", async ({ page }) => {
  await setupClerkTestingToken({ page });

  await page.goto("/");

  await expect(page).toHaveURL(/\/sign-in/);
  await expect(
    page.getByRole("heading", { level: 1, name: "Sign in to Starter" }),
  ).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Email" })).toBeVisible();
  await expectAccessible(page);
});

test("SignIn_EmailCode_ShowsTheCodeStep", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.goto("/sign-in");

  await page.getByRole("textbox", { name: "Email" }).fill(e2eUserEmail());
  await page.getByRole("button", { name: "Email me a code" }).click();

  await expect(
    page.getByRole("heading", { name: "Check your email" }),
  ).toBeVisible();
  await expect(page.getByText(e2eUserEmail())).toBeVisible();
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

test("SignIn_Arabic_IsMirroredAndKeepsTheCodeLeftToRight", async ({
  page,
  context,
  baseURL,
}) => {
  await setupClerkTestingToken({ page });
  // Before sign-in the language is the locale cookie of F13, which the footer's language switcher sets.
  await context.addCookies([
    { name: "NEXT_LOCALE", value: "ar", url: baseURL },
  ]);
  await page.goto("/sign-in");

  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await expect(
    page.getByRole("heading", { level: 1, name: "تسجيل الدخول إلى Starter" }),
  ).toBeVisible();
  await expectAccessible(page);
  await page
    .getByRole("textbox", { name: "البريد الإلكتروني" })
    .fill(e2eUserEmail());
  await page.getByRole("button", { name: "أرسل لي رمزًا" }).click();

  await expect(
    page.getByRole("heading", { name: "تحقّق من بريدك الإلكتروني" }),
  ).toBeVisible();
  await expect(page.locator("[data-slot='input-otp-group']")).toHaveAttribute(
    "dir",
    "ltr",
  );
  await expectAccessible(page);
});
