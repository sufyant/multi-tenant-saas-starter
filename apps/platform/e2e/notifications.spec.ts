import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { mockNotifications, mockPlatform, organisations } from "./api";
import { expectAccessible } from "./axe";

const open = async (page: Page, path = "/") => {
  await mockPlatform(page);
  const notifications = await mockNotifications(page);
  await setupClerkTestingToken({ page });
  await page.goto(path);
  return notifications;
};

const bell = (page: Page, unread: number) =>
  page
    .getByRole("banner")
    .getByRole("button", { name: `Notifications, ${unread} unread` });

test("Bell_Opened_ListsTheNotificationsAndOpensTheOrganisation", async ({
  page,
}) => {
  const { calls } = await open(page);

  await bell(page, 1).click();
  await expect(
    page.getByRole("link", { name: /Shannon Studio was suspended/ }),
  ).toBeVisible();
  await expectAccessible(page);
  await page
    .getByRole("link", { name: /Shannon Studio was suspended/ })
    .click();

  await expect(page).toHaveURL(`/organisations/${organisations.shannon.id}`);
  await expect.poll(() => calls.opened.length).toBe(1);
});

test("Bell_MarkAll_MarksTheBellRead", async ({ page }) => {
  const { calls } = await open(page);

  await bell(page, 1).click();
  await page.getByRole("button", { name: "Mark all as read" }).click();

  await expect(
    page
      .getByRole("banner")
      .getByRole("button", { name: "Notifications", exact: true }),
  ).toBeVisible();
  expect(calls.markedAll).toBe(1);
});

test("Bell_LiveSignal_ChangesTheCountWithoutAReload", async ({ page }) => {
  const notifications = await open(page);
  await expect(bell(page, 1)).toBeVisible();

  notifications.arrive();

  await expect(bell(page, 2)).toBeVisible();
});
