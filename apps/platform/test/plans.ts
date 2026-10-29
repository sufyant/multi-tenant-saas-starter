import {
  EntitlementOverrideResponseState,
  type EntitlementValueResponse,
  FeatureKey,
  LimitKey,
  type ListPageOfPlanListItemResponse,
  type ListPageOfTenantExcessResponse,
  type PlanListItemResponse,
  type PlanResponse,
  type TenantEntitlementResponse,
  type TenantExcessResponse,
  type TenantPlanResponse,
} from "@repo/api-client";
import { HttpResponse, http } from "msw";
import { burren } from "./organisations";

// The plans of the Plans boards (Free, the default; Pro; Partner) and the Plan section of Burren Supply, for the platform's tests.

export const on: EntitlementValueResponse = {
  enabled: true,
  maximum: null,
  unlimited: false,
};
export const off: EntitlementValueResponse = {
  enabled: false,
  maximum: null,
  unlimited: false,
};
export const unlimited: EntitlementValueResponse = {
  enabled: null,
  maximum: null,
  unlimited: true,
};
export const max = (maximum: number): EntitlementValueResponse => ({
  enabled: null,
  maximum,
  unlimited: false,
});

const plan = (
  id: string,
  name: string,
  tenantCount: number,
  entitlements: PlanResponse["entitlements"],
  isDefault = false,
): PlanResponse => ({
  id,
  name,
  isDefault,
  tenantCount,
  entitlements,
  createdAt: "2026-09-01T09:00:00Z",
});

export const free = plan(
  "0198f0a2-0000-7000-8000-0000000000b1",
  "Free",
  1,
  {
    [FeatureKey.RolesCustom]: off,
    [LimitKey.RolesCustomMax]: max(0),
    [LimitKey.MembersMax]: max(5),
  },
  true,
);
export const pro = plan("0198f0a2-0000-7000-8000-0000000000b2", "Pro", 6, {
  [FeatureKey.RolesCustom]: on,
  [LimitKey.RolesCustomMax]: max(20),
  [LimitKey.MembersMax]: unlimited,
});
export const partner = plan(
  "0198f0a2-0000-7000-8000-0000000000b3",
  "Partner",
  0,
  {
    [FeatureKey.RolesCustom]: on,
    [LimitKey.RolesCustomMax]: max(50),
    [LimitKey.MembersMax]: unlimited,
  },
);

export const itemOf = ({
  entitlements: _,
  ...item
}: PlanResponse): PlanListItemResponse => item;

/** One page of ListPlans. */
export const plansPage = (
  plans: PlanResponse[] = [free, pro, partner],
): ListPageOfPlanListItemResponse => ({
  items: plans.map(itemOf),
  page: 1,
  pageSize: 100,
  total: plans.length,
});

/** ListPlans: it records each request's parameters and answers the page. */
export const plansAnswer = (
  page: ListPageOfPlanListItemResponse = plansPage(),
  requests: URLSearchParams[] = [],
) =>
  http.get("*/v1/platform/plans", ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.json(page);
  });

/** GetPlan: it counts the reads and answers the plan. */
export const planAnswer = (
  value: PlanResponse,
  reads: { count: number } = { count: 0 },
) =>
  http.get(`*/v1/platform/plans/${value.id}`, () => {
    reads.count += 1;
    return HttpResponse.json(value);
  });

type Call = { key: string | null; body: unknown; url: URL };

/**
 * A command or a preview of the plans: it records each request's body, key and address, and gives the answers in order (the last
 * one repeats).
 */
export const plansCommand = (
  method: "post" | "patch" | "put" | "delete",
  path: string,
  answers: (() => Response)[],
  calls: Call[] = [],
) =>
  http[method](`*/v1/platform/${path}`, async ({ request }) => {
    const text = await request.text();
    calls.push({
      key: request.headers.get("Idempotency-Key"),
      body: text ? JSON.parse(text) : null,
      url: new URL(request.url),
    });
    const answer = answers[Math.min(calls.length, answers.length) - 1];
    return answer ? answer() : HttpResponse.error();
  });

/** An organisation that a plan edit puts over a limit (the PlanImpact board). */
export const excess = (
  tenantId: string,
  name: string,
  slug: string,
  count: number,
  limit: number,
): TenantExcessResponse => ({
  tenantId,
  name,
  slug,
  furthest: count - limit,
  excesses: [
    {
      key: LimitKey.RolesCustomMax,
      count,
      newValue: max(limit),
      amount: count - limit,
    },
  ],
  overriddenKeys: [],
});

/** One page of GetPlanUpdatePreview. */
export const previewPage = (
  items: TenantExcessResponse[],
  values: Partial<ListPageOfTenantExcessResponse> = {},
): ListPageOfTenantExcessResponse => ({
  items,
  page: 1,
  pageSize: 20,
  total: items.length,
  ...values,
});

/** Ciara Byrne's override of Burren Supply's custom roles limit (the OrgPlan board). */
export const ciaraOverride = (
  values: Partial<NonNullable<TenantEntitlementResponse["override"]>> = {},
): NonNullable<TenantEntitlementResponse["override"]> => ({
  value: max(25),
  expiresAt: "2026-12-31T00:00:00Z",
  state: EntitlementOverrideResponseState.Active,
  reason: "Moving their old roles over; review at year end.",
  setById: "0198f0a2-0000-7000-8000-0000000000c1",
  setBy: { firstName: "Ciara", lastName: "Byrne", deleted: false },
  setAt: "2026-10-16T13:20:00Z",
  ...values,
});

/** Burren Supply on Pro: 14 custom roles and 22 members, with an override of the custom roles limit (the OrgPlan board). */
export const burrenPlan = (
  rows: Partial<Record<string, Partial<TenantEntitlementResponse>>> = {},
): TenantPlanResponse => ({
  tenantId: burren.id,
  plan: { id: pro.id, name: pro.name },
  entitlements: [
    {
      key: FeatureKey.RolesCustom,
      isFeature: true,
      planValue: on,
      override: null,
      effective: on,
      count: 14,
      ...rows[FeatureKey.RolesCustom],
    },
    {
      key: LimitKey.RolesCustomMax,
      isFeature: false,
      planValue: max(20),
      override: ciaraOverride(),
      effective: max(25),
      count: 14,
      ...rows[LimitKey.RolesCustomMax],
    },
    {
      key: LimitKey.MembersMax,
      isFeature: false,
      planValue: unlimited,
      override: null,
      effective: unlimited,
      count: 22,
      ...rows[LimitKey.MembersMax],
    },
  ],
});

/** GetTenantPlan: it counts the reads and answers the plan of the organisation. */
export const tenantPlanAnswer = (
  value: TenantPlanResponse,
  reads: { count: number } = { count: 0 },
) =>
  http.get(`*/v1/platform/tenants/${value.tenantId}/plan`, () => {
    reads.count += 1;
    return HttpResponse.json(value);
  });

/** GetTenantPlan of any organisation: Burren Supply's section, with the organisation's id. */
export const anyTenantPlanAnswer = () =>
  http.get("*/v1/platform/tenants/:tenantId/plan", ({ params }) =>
    HttpResponse.json({ ...burrenPlan(), tenantId: String(params.tenantId) }),
  );
