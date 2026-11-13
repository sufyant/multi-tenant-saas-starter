import {
  type MyTenantResponse,
  type NotificationLinkResponse,
  NotificationLinkResponseKind,
} from "@repo/api-client";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { reportError } from "@/error-tracking/report-error";
import { northwind } from "../../test/tenant";
import { notificationHref } from "./notification-link";

vi.mock("@/error-tracking/report-error", () => ({ reportError: vi.fn() }));
beforeEach(() => vi.clearAllMocks());

const tenants: MyTenantResponse[] = [northwind];
const about = (
  kind: NotificationLinkResponseKind,
  tenantId: string | null = northwind.id,
): NotificationLinkResponse => ({ kind, tenantId });

describe("notificationHref", () => {
  it.each([
    [NotificationLinkResponseKind.TenantMembers, "/northwind/settings/members"],
    [NotificationLinkResponseKind.TenantRoles, "/northwind/settings/roles"],
    [
      NotificationLinkResponseKind.TenantSettings,
      "/northwind/settings/general",
    ],
    // An override of the Activity log's period (#267).
    [
      NotificationLinkResponseKind.TenantActivityLog,
      "/northwind/settings/activity",
    ],
  ])("NotificationHref_%s_LeadsToThatTenantsPageBySlug", (kind, href) => {
    expect(notificationHref(about(kind), tenants)).toBe(href);
    expect(reportError).not.toHaveBeenCalled();
  });

  it("NotificationHref_Start_LeadsToTheStartPage", () => {
    expect(
      notificationHref(
        about(NotificationLinkResponseKind.Start, null),
        tenants,
      ),
    ).toBe("/");
  });

  it("NotificationHref_ATenantThePersonLeft_LeadsToTheStartPage", () => {
    expect(
      notificationHref(about(NotificationLinkResponseKind.TenantMembers), []),
    ).toBe("/");
    expect(reportError).not.toHaveBeenCalled();
  });

  it("NotificationHref_AKindTheWebDoesNotKnow_LeadsToTheStartPageAndIsReportedOnce", () => {
    const unknown = {
      kind: "tenant_billing",
      tenantId: northwind.id,
    } as unknown as NotificationLinkResponse;

    const hrefs = [
      notificationHref(unknown, tenants),
      notificationHref(unknown, tenants),
    ];

    expect(hrefs).toEqual(["/", "/"]);
    expect(reportError).toHaveBeenCalledOnce();
  });

  it("NotificationHref_AccountSecurity_LeadsToAccountAndSecurityWithoutAnOrganisation", () => {
    expect(
      notificationHref(
        about(NotificationLinkResponseKind.AccountSecurity, null),
        tenants,
        northwind.slug,
      ),
    ).toBe("/settings/account");
    expect(reportError).not.toHaveBeenCalled();
  });

  // Inside an organisation the start page would leave the shell and come back to the last organisation used: the person stays in
  // the organisation that is open, on its Home.
  it("NotificationHref_StartInsideAnOrganisation_LeadsToThatOrganisationsHome", () => {
    expect(
      notificationHref(
        about(NotificationLinkResponseKind.Start, null),
        tenants,
        northwind.slug,
      ),
    ).toBe("/northwind");
  });

  it("NotificationHref_ATenantThePersonLeftInsideAnOrganisation_LeadsToThatOrganisationsHome", () => {
    expect(
      notificationHref(
        about(NotificationLinkResponseKind.TenantMembers, "another-tenant"),
        tenants,
        northwind.slug,
      ),
    ).toBe("/northwind");
  });

  it("NotificationHref_AKindTheWebDoesNotKnowInsideAnOrganisation_LeadsToThatOrganisationsHome", () => {
    const unknown = {
      kind: "tenant_usage",
      tenantId: northwind.id,
    } as unknown as NotificationLinkResponse;

    expect(notificationHref(unknown, tenants, northwind.slug)).toBe(
      "/northwind",
    );
  });

  it("NotificationHref_StartInsideAnOrganisationNoLongerInTheList_LeadsToTheStartPage", () => {
    expect(
      notificationHref(
        about(NotificationLinkResponseKind.Start, null),
        [],
        northwind.slug,
      ),
    ).toBe("/");
  });

  it("NotificationHref_APlatformLink_LeadsToTheStartPage", () => {
    expect(
      notificationHref(
        about(NotificationLinkResponseKind.PlatformTenant),
        tenants,
      ),
    ).toBe("/");
  });
});
