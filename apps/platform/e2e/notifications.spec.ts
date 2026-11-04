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
    .getByRole("navigation", { name: "Main menu" })
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

// 3.01 and 3.02 of the platform: the tabs, and the line of the area and the person who acted.
test("Bell_Tabs_ShowTheAreaAndTheActor", async ({ page }) => {
  await open(page);

  await bell(page, 1).click();
  await expect(
    page.getByRole("tab", { name: "Unread 1", selected: true }),
  ).toBeVisible();
  await page.getByRole("tab", { name: "All" }).click();

  await expect(
    page.getByRole("tab", { name: "All", selected: true }),
  ).toBeVisible();
  await expect(page.getByText(/^Organisations · Ciara Byrne · /)).toBeVisible();
  await expectAccessible(page);
});

// 3.08 of the platform: on a phone the bell opens the page of notifications under the top bar.
test("Bell_Phone_OpensThePageOfNotifications", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await open(page);

  await page.getByRole("link", { name: "Notifications, 1 unread" }).click();

  await expect(page).toHaveURL("/notifications");
  await expect(
    page.getByRole("heading", { level: 1, name: "Notifications" }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: /Shannon Studio was suspended/ }),
  ).toBeVisible();
  await expectAccessible(page);
});
