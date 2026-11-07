import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, test } from "@playwright/test";
import { ErrorCode, InvitationResponseState } from "@repo/api-client";
import {
  invitationCode,
  mockInvitation,
  northwind,
  pendingInvitation,
} from "./api";
import { expectAccessible } from "./axe";

const acceptPage = `/invitations/accept?code=${encodeURIComponent(invitationCode)}`;

test.beforeEach(async ({ page }) => {
  await setupClerkTestingToken({ page });
});

test("AcceptInvitation_KnownPerson_JoinsAndOpensTheOrganisation", async ({
  page,
}) => {
  await mockInvitation(page, pendingInvitation);
  await page.goto(acceptPage);

  await expect(
    page.getByRole("heading", { name: "Join Northwind Traders" }),
  ).toBeVisible();
  await expectAccessible(page);
  await page.getByRole("button", { name: "Accept and continue" }).click();

  await expect(page).toHaveURL(`/${northwind.slug}`);
  await expect(page.getByText("You joined Northwind Traders")).toBeVisible();
});

test("AcceptInvitation_NewPerson_AsksForTheNames", async ({ page }) => {
  await mockInvitation(page, pendingInvitation, { knownUser: false });
  await page.goto(acceptPage);

  await expect(page.getByRole("textbox", { name: "Name" })).toBeVisible();
  await expectAccessible(page);
});

test("AcceptInvitation_Expired_SaysItExpired", async ({ page }) => {
  await mockInvitation(page, {
    ...pendingInvitation,
    state: InvitationResponseState.Expired,
  });
  await page.goto(acceptPage);

  await expect(
    page.getByRole("heading", { name: "This invitation has expired" }),
  ).toBeVisible();
  await expectAccessible(page);
});

test("AcceptInvitation_AnotherEmail_OffersToSwitchAccount", async ({
  page,
}) => {
  await mockInvitation(page, ErrorCode.InvitationEmailMismatch);
  await page.goto(acceptPage);

  await expect(
    page.getByRole("heading", {
      name: "This invitation is for another email",
    }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Sign out and continue" }),
  ).toBeVisible();
  await expectAccessible(page);
});

test("AcceptInvitation_UnknownCode_SaysTheLinkDoesNotWork", async ({
  page,
}) => {
  await mockInvitation(page, ErrorCode.InvitationNotFound);
  await page.goto(acceptPage);

  await expect(
    page.getByRole("heading", { name: "This link does not work" }),
  ).toBeVisible();
  await expectAccessible(page);
});

test("AcceptInvitation_Arabic_IsMirrored", async ({
  page,
  context,
  baseURL,
}) => {
  await context.addCookies([
    { name: "NEXT_LOCALE", value: "ar", url: baseURL },
  ]);
  await mockInvitation(page, pendingInvitation);
  await page.goto(acceptPage);

  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await expect(
    page.getByRole("heading", { name: "انضم إلى Northwind Traders" }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "اقبل وتابع" })).toBeVisible();
  await expectAccessible(page);
});
