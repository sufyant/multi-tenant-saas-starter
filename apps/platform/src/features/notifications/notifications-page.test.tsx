import {
  type ChangeEmailPreferencesRequest,
  NotificationCategory,
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

/** GetEmailPreferences of a system admin who belongs to no organisation, and ChangeEmailPreferences, which records each body. */
function preferencesAnswers(sent: ChangeEmailPreferencesRequest[]) {
  return [
    http.get("*/v1/me/email-preferences", () =>
      HttpResponse.json({
        enabled: true,
        categories: [
          { category: NotificationCategory.PlatformTenants, enabled: false },
        ],
      }),
    ),
    http.put("*/v1/me/email-preferences", async ({ request }) => {
      const body = (await request.json()) as ChangeEmailPreferencesRequest;
      sent.push(body);
      return HttpResponse.json(body);
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
    const sent: ChangeEmailPreferencesRequest[] = [];
    server.use(...preferencesAnswers(sent));
    renderWithProviders(<NotificationsPage />);

    await userEvent.click(
      await screen.findByRole("switch", { name: "Platform" }),
    );
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await expectVisibleToast("Notification settings saved.");
    expect(sent).toEqual([
      {
        enabled: true,
        categories: [
          { category: NotificationCategory.PlatformTenants, enabled: true },
        ],
      },
    ]);
  });
});
