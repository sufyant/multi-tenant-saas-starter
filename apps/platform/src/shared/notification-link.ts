import {
  type NotificationLinkResponse,
  NotificationLinkResponseKind,
} from "@repo/api-client";
import { reportError } from "@/error-tracking/report-error";

const start = "/";
const reported = new Set<string>();

/**
 * Where a notification leads on the platform (`apps/api/docs/flows/notifications.md`, section 3): `platform_tenant` opens the
 * organisation's page. A kind the platform does not know goes to the start page, and is reported once (F55).
 */
export function notificationHref(link: NotificationLinkResponse): string {
  if (
    link.kind === NotificationLinkResponseKind.PlatformTenant &&
    link.tenantId
  )
    return `/organisations/${link.tenantId}`;
  if (!reported.has(link.kind)) {
    reported.add(link.kind);
    reportError(
      new Error(`A notification link of an unknown kind: ${link.kind}`),
    );
  }
  return start;
}
