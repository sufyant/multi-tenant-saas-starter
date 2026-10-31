import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import {
  NotificationCategory,
  NotificationChannelKind,
} from "@repo/api-client";
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
    .getByRole("banner")
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

test("NotificationSettings_Save_SendsTheChoice", async ({ page }) => {
  const { calls } = await open(
    page,
    `/${northwind.slug}/settings/notifications`,
  );

  await expect(
    page.getByRole("heading", { level: 1, name: "Notifications" }),
  ).toBeVisible();
  await expectAccessible(page);
  await page.getByRole("switch", { name: "Your access" }).click();
  await page.getByRole("button", { name: "Save" }).click();

  await expect(page.getByText("Notification settings saved.")).toBeVisible();
  expect(calls.saved).toEqual([
    {
      channels: [{ channel: NotificationChannelKind.Email, enabled: true }],
      categories: [
        {
          category: NotificationCategory.Access,
          channel: NotificationChannelKind.Email,
          enabled: true,
        },
        {
          category: NotificationCategory.MemberInvitations,
          channel: NotificationChannelKind.Email,
          enabled: false,
        },
        {
          category: NotificationCategory.TenantPlans,
          channel: NotificationChannelKind.Email,
          enabled: false,
        },
      ],
    },
  ]);
});

test("ManageEmailNotifications_OneOrganisation_OpensItsNotificationsPage", async ({
  page,
}) => {
  await open(page, "/settings/notifications");

  await expect(page).toHaveURL(`/${northwind.slug}/settings/notifications`);
});
