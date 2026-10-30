import {
  BuiltInRole,
  type ChangeEmailPreferencesRequest,
  NotificationCategory,
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

/** GetEmailPreferences, which counts its reads, and ChangeEmailPreferences, which records each body and answers it back. */
function preferencesAnswers(
  sent: ChangeEmailPreferencesRequest[],
  reads = { count: 0 },
) {
  return [
    http.get("*/v1/me/email-preferences", () => {
      reads.count += 1;
      return HttpResponse.json({
        enabled: true,
        categories: [
          { category: NotificationCategory.Access, enabled: false },
          { category: NotificationCategory.TenantPlans, enabled: false },
        ],
      });
    }),
    http.put("*/v1/me/email-preferences", async ({ request }) => {
      const body = (await request.json()) as ChangeEmailPreferencesRequest;
      sent.push(body);
      return HttpResponse.json(body);
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
    const sent: ChangeEmailPreferencesRequest[] = [];
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
        enabled: true,
        categories: [
          { category: NotificationCategory.Access, enabled: true },
          { category: NotificationCategory.TenantPlans, enabled: false },
        ],
      },
    ]);
    expect(reads.count).toBe(2);
  });
});
