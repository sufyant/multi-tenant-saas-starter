import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { mockApi, mockNotifications, northwind } from "./api";
import { expectAccessible } from "./axe";

const open = async (page: Page, path = `/${northwind.slug}`) => {
  await mockApi(page);
  const notifications = await mockNotifications(page);
  await setupClerkTestingToken({ page });
  await page.goto(path);
  return notifications;
};

const bell = (page: Page, unread: number) =>
  page
    .getByRole("navigation", { name: "Main menu" })
    .getByRole("button", { name: `Notifications, ${unread} unread` });

test("Bell_Opened_ListsTheNotificationsAndOpensOnesPage", async ({ page }) => {
  const { calls } = await open(page);

  await bell(page, 1).click();
  await expect(
    page.getByRole("link", { name: /Your role in Northwind Traders changed/ }),
  ).toBeVisible();
  await expectAccessible(page);
  await page
    .getByRole("link", { name: /Your role in Northwind Traders changed/ })
    .click();

  await expect(page).toHaveURL(`/${northwind.slug}/settings/general`);
  await expect.poll(() => calls.opened.length).toBe(1);
});

test("Bell_MarkAll_MarksTheBellRead", async ({ page }) => {
  const { calls } = await open(page);

  await bell(page, 1).click();
  await page.getByRole("button", { name: "Mark all as read" }).click();

  await expect(
    page
      .getByRole("navigation", { name: "Main menu" })
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

// 3.14: under bp-tablet the bell opens a full screen, from the top of the screen to its bottom.
test("Bell_Phone_OpensAFullScreen", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await open(page);

  await page.getByRole("button", { name: "Notifications, 1 unread" }).click();
  const screen = page.getByRole("dialog", { name: "Notifications" });

  await expect
    .poll(async () => await screen.boundingBox())
    .toMatchObject({ y: 0, height: 812 });
});
