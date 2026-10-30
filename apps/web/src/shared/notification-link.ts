import {
  type MyTenantResponse,
  type NotificationLinkResponse,
  NotificationLinkResponseKind,
} from "@repo/api-client";
import { reportError } from "@/error-tracking/report-error";

/** A tenant page of a link kind, under the tenant's slug (F30). */
const tenantPages: Partial<Record<NotificationLinkResponseKind, string>> = {
  [NotificationLinkResponseKind.TenantMembers]: "settings/members",
  [NotificationLinkResponseKind.TenantRoles]: "settings/roles",
  // General shows "Your role" and the plan to every role (`apps/api/docs/flows/notifications.md`, section 1).
  [NotificationLinkResponseKind.TenantSettings]: "settings/general",
};

const start = "/";
const reported = new Set<string>();

/**
 * Where a notification leads in the tenant web (`apps/api/docs/flows/notifications.md`, section 3): the API names a kind and a
 * tenant, and the web builds the URL by the tenant's slug from ListMyTenants. `start` and a tenant the person no longer belongs to
 * go to the start page, which opens the one organisation left, the choice of organisations or "No access yet". A kind the web does
 * not know goes there too, and is reported once (F55).
 */
export function notificationHref(
  link: NotificationLinkResponse,
  tenants: readonly MyTenantResponse[],
): string {
  if (link.kind === NotificationLinkResponseKind.Start) return start;
  const page = tenantPages[link.kind];
  if (!page) {
    if (!reported.has(link.kind)) {
      reported.add(link.kind);
      reportError(
        new Error(`A notification link of an unknown kind: ${link.kind}`),
      );
    }
    return start;
  }
  const tenant = tenants.find((each) => each.id === link.tenantId);
  return tenant ? `/${tenant.slug}/${page}` : start;
}
