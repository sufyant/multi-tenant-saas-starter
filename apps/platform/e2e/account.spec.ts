import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { Language } from "@repo/api-client";
import { mockApi } from "./api";
import { expectAccessible } from "./axe";
import { mockIdentity } from "./identity";

// Account and security (F62): the API's parts are mocked; the identity provider's parts read the real development instance, where
// the setup's temporary user has this one session and an authenticator.
test("Account_English_SaysTwoStepIsOnAndShowsThisDevice", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.En);

  await page.goto("/settings/account");

  await expect(
    page.getByRole("heading", { level: 1, name: "Account and security" }),
  ).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Two-step verification" }),
  ).toContainText("On");
  await expect(page.getByText("This device", { exact: true })).toBeVisible();
  await expectAccessible(page);
});

// The frame's state of 6.01 and 9.01, from Clerk's changed answers (e2e/identity.ts): Google connected, two other sessions.
test("Account_ConnectedGoogleAndOtherSessions_OffersDisconnectAndSignOut", async ({
  page,
}) => {
  await setupClerkTestingToken({ page });
  await mockApi(page, Language.En);
  await mockIdentity(page, {
    googleEmail: "deniz.kaya@gmail.example",
    otherSessions: true,
  });

  await page.goto("/settings/account");

  const methods = page.getByRole("region", { name: "Sign-in methods" });
  await expect(methods).toContainText("deniz.kaya@gmail.example");
  await expect(
    methods.getByRole("button", { name: "Disconnect" }),
  ).toBeVisible();
  const sessions = page.getByRole("region", { name: "Sessions" });
  await expect(
    sessions.getByText("Firefox on Windows", { exact: true }),
  ).toBeVisible();
  await expect(
    sessions.getByRole("button", { name: "Sign out", exact: true }),
  ).toHaveCount(2);
  await expect(
    sessions.getByRole("button", { name: "Sign out of all other sessions" }),
  ).toBeVisible();
  await expectAccessible(page);
});
