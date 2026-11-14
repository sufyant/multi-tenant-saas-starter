import type { Page } from "@playwright/test";
import {
  AuditActorResponseKind,
  type AuditEntryResponse,
  AuditOperation,
  AuditTargetResponseKind,
  EntitlementOverrideResponseState,
  type EntitlementValueResponse,
  FeatureKey,
  InvitationResponseState,
  Language,
  LimitKey,
  TenantCreatedDetailsResponseOperation,
  type TenantDetailResponse,
  TenantEntitlementOverrideSetDetailsResponseOperation,
  type TenantEntitlementResponse,
  type TenantListItemResponse,
  type TenantPlanResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import { tenantsPage } from "./api";

// The Organisations frames (4.01 to 4.26, 4.43, 4.44, 4.46): the organisations, their plans and their entries as the frames draw
// them. The dates of the frames stay as drawn, except the Owner invitation's, which the frame draws from today (4.11).

const deniz = { name: "Deniz Kaya", deleted: false };
const day = 24 * 60 * 60 * 1000;
const inDays = (days: number) =>
  new Date(Date.now() + days * day).toISOString();
const today = (hours: number, minutes: number) => {
  const at = new Date();
  at.setHours(hours, minutes, 0, 0);
  return at.toISOString();
};

const value = (
  values: Partial<EntitlementValueResponse>,
): EntitlementValueResponse => ({
  enabled: null,
  maximum: null,
  unlimited: false,
  ...values,
});

const planNames = {
  free: { id: "0198f0a2-0000-7000-8000-00000000c101", name: "Free" },
  team: { id: "0198f0a2-0000-7000-8000-00000000c102", name: "Team" },
  business: { id: "0198f0a2-0000-7000-8000-00000000c103", name: "Business" },
};

const detail = (
  id: number,
  name: string,
  slug: string,
  plan: keyof typeof planNames,
  fields: Partial<TenantDetailResponse> = {},
): TenantDetailResponse & {
  plan: (typeof planNames)[keyof typeof planNames];
} => ({
  id: `0198f0a2-0000-7000-8000-0000000d${String(id).padStart(4, "0")}`,
  name,
  slug,
  status: TenantResponseStatus.Active,
  createdAt: "2026-03-12T08:40:00Z",
  suspensionReason: null,
  closedAt: null,
  purgedAt: null,
  reopenableUntil: null,
  purgeWarningAt: null,
  memberCount: 0,
  ownerCount: 1,
  owners: [],
  ownerInvitation: null,
  pendingInvitationCount: 0,
  createdBy: deniz,
  suspendedAt: null,
  suspendedBy: null,
  closedBy: null,
  purgeStartedAt: null,
  plan: planNames[plan],
  ...fields,
});

const owner = (id: number, name: string, email: string) => ({
  userId: `0198f0a2-0000-7000-8000-0000000b${String(id).padStart(4, "0")}`,
  name,
  email,
});

/** The organisations of 4.01, in the frame's order, with Greyfield Books, which only the Deleted tab lists (4.26). */
export const frameOrganisations = {
  northwind: detail(1, "Northwind Traders", "northwind", "team", {
    memberCount: 10,
    pendingInvitationCount: 2,
    createdBy: null,
    owners: [owner(1, "Ayşe Demir", "ayse@northwind.example")],
  }),
  pacific: detail(
    2,
    "Pacific Coast Freight & Warehousing Cooperative Ltd.",
    "pacific-coast",
    "business",
    { memberCount: 24 },
  ),
  ufuq: detail(3, "شركة الأفق للتجارة", "al-ufuq", "business", {
    memberCount: 8,
  }),
  harbourside: detail(4, "Harbourside Dental", "harbourside", "free", {
    createdAt: "2026-11-02T09:00:00Z",
    ownerCount: 0,
    pendingInvitationCount: 1,
    ownerInvitation: {
      email: "maya.chen@harbourside.example",
      expiresAt: inDays(7),
      state: InvitationResponseState.Pending,
      language: Language.En,
      sentAt: today(10, 12),
    },
  }),
  lindqvist: detail(5, "Lindqvist Design Studio", "lindqvist", "team", {
    memberCount: 6,
  }),
  marrow: detail(6, "Marrow & Vine Catering", "marrow-vine", "team", {
    memberCount: 4,
  }),
  copperleaf: detail(7, "Copperleaf Tutoring", "copperleaf", "free", {
    memberCount: 3,
  }),
  blueHarbour: detail(8, "Blue Harbour Logistics", "blue-harbour", "team", {
    status: TenantResponseStatus.Suspended,
    createdAt: "2026-01-09T09:00:00Z",
    memberCount: 5,
    owners: [owner(8, "Aiko Tanaka", "aiko@blueharbour.example")],
    suspensionReason: "unpaid invoices",
    suspendedAt: "2026-10-30T10:00:00Z",
    suspendedBy: deniz,
  }),
  fernwood: detail(9, "Fernwood Studio", "fernwood", "team", {
    status: TenantResponseStatus.Closed,
    createdAt: "2026-06-02T09:00:00Z",
    memberCount: 3,
    owners: [owner(9, "Jonah Reyes", "jonah@fernwood.example")],
    closedAt: "2026-11-01T10:00:00Z",
    closedBy: deniz,
    reopenableUntil: "2026-12-01T10:00:00Z",
  }),
  oldMill: detail(10, "Old Mill Bakery", "old-mill", "team", {
    status: TenantResponseStatus.Purging,
    createdAt: "2026-05-15T09:00:00Z",
    memberCount: 2,
    owners: [owner(10, "Ruth Okoye", "ruth@oldmill.example")],
    closedAt: "2026-10-02T10:00:00Z",
    purgeStartedAt: "2026-11-01T02:00:00Z",
  }),
  greyfield: detail(11, "Greyfield Books", "greyfield", "team", {
    status: TenantResponseStatus.Purged,
    createdAt: "2026-02-04T09:00:00Z",
    createdBy: null,
    ownerCount: 0,
    purgedAt: "2026-10-12T02:00:00Z",
  }),
};

export const frameNorthwind = frameOrganisations.northwind;

const listItemOf = ({
  owners: _,
  purgeWarningAt: __,
  createdBy: ___,
  suspendedAt: ____,
  suspendedBy: _____,
  closedBy: ______,
  purgeStartedAt: _______,
  ...item
}: (typeof frameOrganisations)[keyof typeof frameOrganisations]): TenantListItemResponse =>
  item;

/** A key of an organisation's plan: the plan's value, the override, the value that applies and the count. */
const key = (
  entitlementKey: string,
  isFeature: boolean,
  planValue: EntitlementValueResponse,
  count: number,
  override: TenantEntitlementResponse["override"] = null,
): TenantEntitlementResponse => ({
  key: entitlementKey,
  isFeature,
  planValue,
  override,
  effective: override?.value ?? planValue,
  count,
});

/**
 * Northwind's override of the Activity log, which 4.17 removes: 730 days until 31 Jan 2027. 4.14 draws the period with no
 * override.
 */
const longerRetention: TenantEntitlementResponse["override"] = {
  value: value({ maximum: 730 }),
  expiresAt: "2027-01-31T00:00:00Z",
  state: EntitlementOverrideResponseState.Active,
  reason: "Legal review",
  setById: "0198f0a2-0000-7000-8000-00000000b0d1",
  setBy: deniz,
  setAt: "2026-10-28T10:20:00Z",
};

/** Northwind's override that turns Custom roles off (4.50 to 4.52). */
const customRolesPaused: TenantEntitlementResponse["override"] = {
  value: value({ enabled: false }),
  expiresAt: null,
  state: EntitlementOverrideResponseState.Active,
  reason: "contract paused",
  setById: "0198f0a2-0000-7000-8000-00000000b0d1",
  setBy: deniz,
  setAt: "2026-11-02T09:30:00Z",
};

/** The overrides a shot gives Northwind beside its override of the custom roles limit. */
type FrameOverrides = {
  /** The Activity log's override, which 4.17 removes. */
  retentionOverride?: boolean;
  /** Custom roles turned off by an override (4.50 to 4.52). */
  customRolesOff?: boolean;
  /** The override of the custom roles limit; 4.51 draws the limit without it. */
  limitOverride?: boolean;
};

/**
 * The organisation's plan: Team gives 15 seats, 5 custom roles and an Activity log of 365 days, Free 3 seats and 90 days, Business
 * no limit of seats and 365 days (4.14, 4.15).
 */
const planOf = (
  organisation: (typeof frameOrganisations)[keyof typeof frameOrganisations],
  {
    retentionOverride = false,
    customRolesOff = false,
    limitOverride = true,
  }: FrameOverrides,
): TenantPlanResponse => {
  const northwind = organisation.id === frameNorthwind.id;
  const seats = organisation.memberCount + organisation.pendingInvitationCount;
  const team = organisation.plan.id === planNames.team.id;
  const free = organisation.plan.id === planNames.free.id;
  return {
    tenantId: organisation.id,
    plan: organisation.plan,
    entitlements: [
      key(
        FeatureKey.RolesCustom,
        true,
        value({ enabled: !free }),
        team ? 4 : 0,
        customRolesOff && northwind ? customRolesPaused : null,
      ),
      key(
        LimitKey.RolesCustomMax,
        false,
        value(free ? { maximum: 0 } : team ? { maximum: 5 } : { maximum: 20 }),
        team ? 4 : 0,
        limitOverride && northwind
          ? {
              value: value({ maximum: 10 }),
              expiresAt: "2026-12-31T00:00:00Z",
              state: EntitlementOverrideResponseState.Active,
              reason: "pilot",
              setById: "0198f0a2-0000-7000-8000-00000000b0d1",
              setBy: deniz,
              setAt: "2026-10-28T10:15:00Z",
            }
          : null,
      ),
      key(
        LimitKey.MembersMax,
        false,
        value(
          free ? { maximum: 3 } : team ? { maximum: 15 } : { unlimited: true },
        ),
        seats,
      ),
      key(
        LimitKey.AuditRetentionDays,
        false,
        value({ maximum: free ? 90 : 365 }),
        0,
        retentionOverride && northwind ? longerRetention : null,
      ),
    ],
  };
};

const platform = {
  kind: AuditActorResponseKind.Platform,
  userId: null,
  displayName: null,
  deleted: null,
};

/** The platform's entries about Northwind (4.18): its creation and the override; the tenant's own entries are not the platform's. */
const northwindLog: AuditEntryResponse[] = [
  {
    id: "0198f0a2-0000-7000-8000-0000000f0001",
    occurredAt: "2026-10-28T10:15:00Z",
    operation: AuditOperation.TenantEntitlementOverrideSet,
    actor: platform,
    target: {
      kind: AuditTargetResponseKind.Tenant,
      id: frameNorthwind.id,
      name: frameNorthwind.name,
      deleted: null,
      email: null,
    },
    details: {
      operation:
        TenantEntitlementOverrideSetDetailsResponseOperation.TenantEntitlementOverrideSet,
      key: LimitKey.RolesCustomMax,
      oldValue: value({ maximum: 5 }),
      oldExpiresAt: null,
      newValue: value({ maximum: 10 }),
      newExpiresAt: "2026-12-31T00:00:00Z",
      reason: "pilot",
    },
  },
  {
    id: "0198f0a2-0000-7000-8000-0000000f0002",
    occurredAt: "2026-03-12T08:40:00Z",
    operation: AuditOperation.TenantCreated,
    actor: {
      kind: AuditActorResponseKind.User,
      userId: "0198f0a2-0000-7000-8000-00000000b0d1",
      displayName: "Deniz Kaya",
      deleted: false,
    },
    target: {
      kind: AuditTargetResponseKind.Tenant,
      id: frameNorthwind.id,
      name: frameNorthwind.name,
      deleted: null,
      email: null,
    },
    details: {
      operation: TenantCreatedDetailsResponseOperation.TenantCreated,
      plan: planNames.team,
    },
  },
];

/**
 * ListTenants, GetTenant, GetTenantPlan and the organisation's entries answer the frames' organisations; a command goes on to the
 * shot's own route. `items` gives the list another set of organisations, such as none for 4.06; `overrides` gives Northwind the
 * override of the Activity log that 4.17 removes, or Custom roles turned off (4.50 to 4.52).
 */
export async function mockFrameOrganisations(
  page: Page,
  items: TenantListItemResponse[] = Object.values(frameOrganisations).map(
    listItemOf,
  ),
  overrides: FrameOverrides = {},
): Promise<void> {
  const all = Object.values(frameOrganisations);
  await page.route("**/api/v1/platform/tenants*", (route) =>
    route.request().method() === "GET"
      ? route.fulfill({
          json: tenantsPage(items, new URL(route.request().url()).searchParams),
        })
      : route.fallback(),
  );
  await page.route("**/api/v1/platform/tenants/*", (route) => {
    if (route.request().method() !== "GET") return route.fallback();
    const id = new URL(route.request().url()).pathname.split("/").at(-1);
    const found = all.find((organisation) => organisation.id === id);
    if (!found) return route.fallback();
    const { plan: _, ...tenant } = found;
    return route.fulfill({ json: tenant });
  });
  await page.route("**/api/v1/platform/tenants/*/plan", (route) => {
    if (route.request().method() !== "GET") return route.fallback();
    const id = new URL(route.request().url()).pathname.split("/").at(-2);
    const found = all.find((organisation) => organisation.id === id);
    return found
      ? route.fulfill({ json: planOf(found, overrides) })
      : route.fallback();
  });
  await page.route("**/api/v1/platform/plans*", (route) =>
    route.request().method() === "GET"
      ? route.fulfill({
          json: {
            items: Object.values(planNames).map((plan) => ({
              ...plan,
              isDefault: plan.id === planNames.free.id,
              tenantCount: 1,
              createdAt: "2026-01-01T09:00:00Z",
            })),
            page: 1,
            pageSize: 100,
            total: 3,
          },
        })
      : route.fallback(),
  );
  await page.route("**/api/v1/platform/audit-log*", (route) =>
    route.fulfill({
      json: {
        items:
          new URL(route.request().url()).searchParams.get("tenantId") ===
          frameNorthwind.id
            ? northwindLog
            : [],
        nextCursor: null,
      },
    }),
  );
}

/** The preview of a change to Free (4.15): the seats go over, custom roles turn off, the override stays, the Activity log gets shorter. */
export const freePreview = {
  excesses: [
    {
      key: FeatureKey.RolesCustom,
      count: 4,
      newValue: value({ enabled: false }),
      amount: 4,
    },
    {
      key: LimitKey.MembersMax,
      count: 12,
      newValue: value({ maximum: 3 }),
      amount: 9,
    },
  ],
  overriddenKeys: [LimitKey.RolesCustomMax],
  retentionShortened: { fromDays: 365, toDays: 90 },
};

export const freePlanId = planNames.free.id;
