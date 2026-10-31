import {
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
import { renderWithProviders } from "../../../test/render";
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

/** The table of a system admin who belongs to no organisation: the required row, then the platform's. */
const adminsTable = {
  channels: [
    { channel: NotificationChannelKind.Web, enabled: true },
    { channel: NotificationChannelKind.Email, enabled: true },
  ],
  categories: [
    row(NotificationCategory.AccountSecurity, true, true),
    row(NotificationCategory.PlatformTenants, false),
  ],
};

/** GetNotificationPreferences of that system admin, and ChangeNotificationPreferences, which records each body. */
function preferencesAnswers(sent: ChangeNotificationPreferencesRequest[]) {
  return [
    http.get("*/v1/me/notification-preferences", () =>
      HttpResponse.json(adminsTable),
    ),
    http.put("*/v1/me/notification-preferences", async ({ request }) => {
      sent.push((await request.json()) as ChangeNotificationPreferencesRequest);
      return HttpResponse.json(adminsTable);
    }),
  ];
}

describe("NotificationsPage", () => {
  it("NotificationsPage_SystemAdmin_ShowsPlatformAndNoRemovalLine", async () => {
    server.use(...preferencesAnswers([]));

    renderWithProviders(<NotificationsPage />);

    expect(
      await screen.findByRole("switch", { name: "Platform" }),
    ).not.toBeChecked();
    expect(
      within(screen.getByRole("navigation", { name: "Settings" })).getByRole(
        "link",
        { name: "Notifications" },
      ),
    ).toHaveAttribute("href", "/settings/notifications");
    expect(
      screen.queryByText(
        "We always email you if you are removed from an organisation.",
      ),
    ).toBeNull();
    expect(
      screen.getByText(
        "You always see notifications in the app. Choose which ones we also send by email. These settings are yours, the same on the platform and in every organisation.",
      ),
    ).toBeVisible();
  });

  it("NotificationsPage_Save_SendsTheChoiceAndConfirms", async () => {
    const sent: ChangeNotificationPreferencesRequest[] = [];
    server.use(...preferencesAnswers(sent));
    renderWithProviders(<NotificationsPage />);

    await userEvent.click(
      await screen.findByRole("switch", { name: "Platform" }),
    );
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await expectVisibleToast("Notification settings saved.");
    expect(sent).toEqual([
      {
        channels: [{ channel: NotificationChannelKind.Email, enabled: true }],
        categories: [
          {
            category: NotificationCategory.PlatformTenants,
            channel: NotificationChannelKind.Email,
            enabled: true,
          },
        ],
      },
    ]);
  });
});
