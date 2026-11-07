import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import { NotificationLinkResponseKind } from "@repo/api-client";
import { mockApi, mockNotifications, northwind, roleChanged } from "./api";
import { expectAccessible } from "./axe";

/** The bell's tabs; a page may have tabs of its own. */
const bellTabs = (page: Page) =>
  page.getByRole("tablist", { name: "Which notifications" });

const open = async (
  page: Page,
  path = `/${northwind.slug}`,
  items = [roleChanged],
) => {
  await mockApi(page);
  const notifications = await mockNotifications(page, { items });
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

// 3.01 and 3.02: the bell opens on Unread, which reads the unread items, and All reads every item.
test("Bell_Tabs_ShowTheUnreadOnesThenEveryOne", async ({ page }) => {
  await open(page);

  await bell(page, 1).click();
  await expect(
    bellTabs(page).getByRole("tab", { name: "Unread 1", selected: true }),
  ).toBeVisible();
  await bellTabs(page).getByRole("tab", { name: "All" }).click();

  await expect(
    bellTabs(page).getByRole("tab", { name: "All", selected: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: /Your role in Northwind Traders changed/ }),
  ).toBeVisible();
  await expect(
    page.getByText(/^Northwind Traders · Your access · /),
  ).toBeVisible();
  await expectAccessible(page);
});

// The bug of staging: a link of the kind start went to "/", outside the tenant layout, so the shell unmounted and "/" sent the person
// back. Opening a notification inside an organisation keeps the shell: the sidebar that was marked before the click is still there.
test("Bell_AStartLinkInsideAnOrganisation_KeepsTheShellMounted", async ({
  page,
}) => {
  const removed = {
    ...roleChanged,
    title: "You were removed from Harbour Logistics",
    tenantName: "Harbour Logistics",
    link: { kind: NotificationLinkResponseKind.Start, tenantId: null },
  };
  await open(page, `/${northwind.slug}`, [removed]);
  await page
    .getByRole("navigation", { name: "Main menu" })
    .evaluate((element) => {
      element.setAttribute("data-e2e-mounted", "before");
    });

  await bell(page, 1).click();
  await page
    .getByRole("link", { name: /You were removed from Harbour Logistics/ })
    .click();

  await expect(page).toHaveURL(`/${northwind.slug}`);
  await expect(page.locator("[data-e2e-mounted=before]")).toHaveCount(1);
});
