import {
  type MyTenantResponse,
  type NotificationLinkResponse,
  NotificationLinkResponseKind,
} from "@repo/api-client";
import { reportError } from "@/error-tracking/report-error";
import { personalPages } from "./personal-pages";

/** A tenant page of a link kind, under the tenant's slug (F30). */
const tenantPages: Partial<Record<NotificationLinkResponseKind, string>> = {
  [NotificationLinkResponseKind.TenantMembers]: "settings/members",
  [NotificationLinkResponseKind.TenantRoles]: "settings/roles",
  // General shows "Your role" and the plan to every role (`apps/api/docs/flows/notifications.md`, section 1).
  [NotificationLinkResponseKind.TenantSettings]: "settings/general",
  // An override of the Activity log's period (37c).
  [NotificationLinkResponseKind.TenantActivityLog]: "settings/activity",
};

const start = "/";
const reported = new Set<string>();

/**
 * Where a notification leads in the tenant web (`apps/api/docs/flows/notifications.md`, section 3): the API names a kind and a
 * tenant, and the web builds the URL by the tenant's slug from ListMyTenants. `account_security` leads to Account and security.
 * `start`, a tenant the person no longer belongs to and a kind the web does not know lead to the start page, which opens the one
 * organisation left, the choice of organisations or "No access yet". Inside an organisation that is still the person's, they
 * lead to its Home instead: the start page is outside the tenant layout, so the shell would unmount and come back. A kind the
 * web does not know is reported once (F55).
 */
export function notificationHref(
  link: NotificationLinkResponse,
  tenants: readonly MyTenantResponse[],
  /** The slug of the organisation whose shell holds the bell; none for the personal bell. */
  current?: string,
): string {
  if (link.kind === NotificationLinkResponseKind.AccountSecurity)
    return personalPages.account;
  const home = tenants.some((tenant) => tenant.slug === current)
    ? `/${current}`
    : start;
  if (link.kind === NotificationLinkResponseKind.Start) return home;
  const page = tenantPages[link.kind];
  if (!page) {
    if (!reported.has(link.kind)) {
      reported.add(link.kind);
      reportError(
        new Error(`A notification link of an unknown kind: ${link.kind}`),
      );
    }
    return home;
  }
  const tenant = tenants.find((each) => each.id === link.tenantId);
  return tenant ? `/${tenant.slug}/${page}` : home;
}
