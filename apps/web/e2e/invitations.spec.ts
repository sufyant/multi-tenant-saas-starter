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
    page.getByRole("heading", {
      name: "Ayşe Yılmaz invited you to join Northwind MRO",
    }),
  ).toBeVisible();
  await expectAccessible(page);
  await page.getByRole("button", { name: "Join Northwind MRO" }).click();

  await expect(page).toHaveURL(`/${northwind.slug}`);
  await expect(page.getByText("You joined Northwind MRO")).toBeVisible();
});

test("AcceptInvitation_NewPerson_AsksForTheNames", async ({ page }) => {
  await mockInvitation(page, pendingInvitation, { knownUser: false });
  await page.goto(acceptPage);

  await expect(page.getByRole("textbox", { name: "First name" })).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Last name" })).toBeVisible();
  await expectAccessible(page);
});

test("AcceptInvitation_Expired_SaysItExpired", async ({ page }) => {
  await mockInvitation(page, {
    ...pendingInvitation,
    state: InvitationResponseState.Expired,
  });
  await page.goto(acceptPage);

  await expect(
    page.getByRole("heading", { name: "This invitation expired" }),
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
      name: "This invitation is for a different email",
    }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Switch account" }),
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
    page.getByRole("button", { name: "انضم إلى Northwind MRO" }),
  ).toBeVisible();
  await expectAccessible(page);
});
