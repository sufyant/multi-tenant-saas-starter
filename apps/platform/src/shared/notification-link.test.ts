import {
  type NotificationLinkResponse,
  NotificationLinkResponseKind,
} from "@repo/api-client";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { reportError } from "@/error-tracking/report-error";
import { notificationHref } from "./notification-link";

vi.mock("@/error-tracking/report-error", () => ({ reportError: vi.fn() }));
beforeEach(() => vi.clearAllMocks());

const tenantId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";

describe("notificationHref", () => {
  it("NotificationHref_PlatformTenant_LeadsToTheOrganisationsPage", () => {
    expect(
      notificationHref({
        kind: NotificationLinkResponseKind.PlatformTenant,
        tenantId,
      }),
    ).toBe(`/organisations/${tenantId}`);
  });

  it("NotificationHref_AKindThePlatformDoesNotKnow_LeadsToTheStartPageAndIsReportedOnce", () => {
    const unknown = {
      kind: "platform_billing",
      tenantId,
    } as unknown as NotificationLinkResponse;

    const hrefs = [notificationHref(unknown), notificationHref(unknown)];

    expect(hrefs).toEqual(["/", "/"]);
    expect(reportError).toHaveBeenCalledOnce();
  });
});
