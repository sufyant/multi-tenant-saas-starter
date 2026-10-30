import { BuiltInRole, Permission } from "@repo/api-client";
import { screen, within } from "@testing-library/react";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";
import { renderInTenant, tenantAnswers } from "../../test/tenant";
import { SettingsNav } from "./settings-nav";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const links = async () =>
  within(await screen.findByRole("navigation", { name: "Settings" }))
    .getAllByRole("link")
    .map((link) => [link.textContent, link.getAttribute("href")]);

describe("SettingsNav", () => {
  it("SettingsNav_Owner_ShowsGeneralMembersRolesActivityPreferencesAndNotifications", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<SettingsNav current="general" />);

    expect(await links()).toEqual([
      ["General", "/northwind/settings/general"],
      ["Members", "/northwind/settings/members"],
      ["Roles", "/northwind/settings/roles"],
      ["Activity", "/northwind/settings/activity"],
      ["Preferences", "/northwind/settings/preferences"],
      ["Notifications", "/northwind/settings/notifications"],
    ]);
  });

  it("SettingsNav_Admin_ShowsActivity", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin));

    renderInTenant(<SettingsNav current="activity" />);

    expect(
      await screen.findByRole("link", { name: "Activity" }),
    ).toHaveAttribute("aria-current", "page");
  });

  it("SettingsNav_Admin_ShowsRolesByItsReadPermission", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin));

    renderInTenant(<SettingsNav current="roles" />);

    expect(await screen.findByRole("link", { name: "Roles" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("SettingsNav_Member_LeavesOutRolesAndActivity", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member));

    renderInTenant(<SettingsNav current="general" />);

    expect(await links()).toEqual([
      ["General", "/northwind/settings/general"],
      ["Members", "/northwind/settings/members"],
      ["Preferences", "/northwind/settings/preferences"],
      ["Notifications", "/northwind/settings/notifications"],
    ]);
  });

  it("SettingsNav_NoOrganisationPermission_ShowsOnlyYou", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member, []));

    renderInTenant(<SettingsNav current="preferences" />);

    expect(await links()).toEqual([
      ["Preferences", "/northwind/settings/preferences"],
      ["Notifications", "/northwind/settings/notifications"],
    ]);
    expect(screen.queryByText("Organisation")).not.toBeInTheDocument();
  });

  it("SettingsNav_ProfileReadOnly_ShowsGeneral", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Member, [Permission.TenantProfileRead]),
    );

    renderInTenant(<SettingsNav current="general" />);

    expect(await links()).toEqual([
      ["General", "/northwind/settings/general"],
      ["Preferences", "/northwind/settings/preferences"],
      ["Notifications", "/northwind/settings/notifications"],
    ]);
  });
});
