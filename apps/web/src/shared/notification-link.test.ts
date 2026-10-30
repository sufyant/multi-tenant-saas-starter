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
  ])("NotificationHref_%s_LeadsToThatTenantsPageBySlug", (kind, href) => {
    expect(notificationHref(about(kind), tenants)).toBe(href);
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

  it("NotificationHref_APlatformLink_LeadsToTheStartPage", () => {
    expect(
      notificationHref(
        about(NotificationLinkResponseKind.PlatformTenant),
        tenants,
      ),
    ).toBe("/");
  });
});
