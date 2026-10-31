import {
  BuiltInRole,
  type ChangeNotificationPreferencesRequest,
  NotificationCategory,
  NotificationChannelKind,
} from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";
import { renderInTenant, tenantAnswers } from "../../../test/tenant";
import { NotificationsPage } from "./notifications-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

/** A row of the table with its web on and its email as given. */
const row = (
  category: NotificationCategory,
  email: boolean,
  required = false,
) => ({
  category,
  required,
  types: [],
  channels: [
    { channel: NotificationChannelKind.Web, enabled: true },
    { channel: NotificationChannelKind.Email, enabled: email },
  ],
});

/** The table of an Owner: the column's main switches and the rows that reach them, the required one first. */
const ownersTable = {
  channels: [
    { channel: NotificationChannelKind.Web, enabled: true },
    { channel: NotificationChannelKind.Email, enabled: true },
  ],
  categories: [
    row(NotificationCategory.AccountSecurity, true, true),
    row(NotificationCategory.Access, false),
    row(NotificationCategory.TenantPlans, false),
  ],
};

/** GetNotificationPreferences, which counts its reads, and ChangeNotificationPreferences, which records each body. */
function preferencesAnswers(
  sent: ChangeNotificationPreferencesRequest[],
  reads = { count: 0 },
) {
  return [
    http.get("*/v1/me/notification-preferences", () => {
      reads.count += 1;
      return HttpResponse.json(ownersTable);
    }),
    http.put("*/v1/me/notification-preferences", async ({ request }) => {
      sent.push((await request.json()) as ChangeNotificationPreferencesRequest);
      return HttpResponse.json(ownersTable);
    }),
  ];
}

const url = "/northwind/settings/notifications";

describe("NotificationsPage", () => {
  it("NotificationsPage_Opened_ShowsTheSubNavAndTheListedCategories", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), ...preferencesAnswers([]));

    renderInTenant(<NotificationsPage />, { url });

    expect(
      await screen.findByRole("heading", { level: 1, name: "Notifications" }),
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole("navigation", { name: "Settings" })).getByRole(
        "link",
        { name: "Notifications" },
      ),
    ).toHaveAttribute("aria-current", "page");
    expect(
      await screen.findByRole("switch", { name: "Organisation plan" }),
    ).not.toBeChecked();
    expect(screen.queryByRole("switch", { name: "Members" })).toBeNull();
  });

  it("NotificationsPage_Save_SendsTheChoiceConfirmsAndReadsAgain", async () => {
    const sent: ChangeNotificationPreferencesRequest[] = [];
    const reads = { count: 0 };
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      ...preferencesAnswers(sent, reads),
    );
    renderInTenant(<NotificationsPage />, { url });

    await userEvent.click(
      await screen.findByRole("switch", { name: "Your access" }),
    );
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await expectVisibleToast("Notification settings saved.");
    expect(sent).toEqual([
      {
        channels: [{ channel: NotificationChannelKind.Email, enabled: true }],
        categories: [
          {
            category: NotificationCategory.Access,
            channel: NotificationChannelKind.Email,
            enabled: true,
          },
          {
            category: NotificationCategory.TenantPlans,
            channel: NotificationChannelKind.Email,
            enabled: false,
          },
        ],
      },
    ]);
    expect(reads.count).toBe(2);
  });
});
