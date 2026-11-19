import {
  type BillingResponse,
  BillingResponseBilledBy,
  type PlanChoiceResponse,
  type SubscriptionResponse,
  SubscriptionResponseInterval,
  SubscriptionResponseStatus,
} from "@repo/api-client";
import { HttpResponse, http } from "msw";
import { grace, northwind, people, roleOf } from "./tenant";

// Northwind's billing as the Billing frames draw it (10.01): Team, monthly, €10 per person, ten people and Grace, its Billing manager.

const plan = (
  id: string,
  name: string,
  prices: PlanChoiceResponse["prices"],
  entitlements: PlanChoiceResponse["entitlements"],
  isDefault = false,
): PlanChoiceResponse => ({
  id,
  name,
  isDefault,
  entitlements,
  currency: "EUR",
  prices,
  meters: [],
});

export const freePlan = plan(
  "0198f0a2-0000-7000-8000-0000000000a1",
  "Free",
  { baseMonthly: 0, perPersonMonthly: 0, baseYearly: 0, perPersonYearly: 0 },
  {
    "members.max": 3,
    "roles.custom": false,
    "roles.custom.max": 0,
    "audit.retention.days": 90,
  },
  true,
);
export const teamPlan = plan(
  "0198f0a2-0000-7000-8000-0000000000a2",
  "Team",
  {
    baseMonthly: 0,
    perPersonMonthly: 1000,
    baseYearly: 0,
    perPersonYearly: 10000,
  },
  {
    "members.max": 15,
    "roles.custom": true,
    "roles.custom.max": 5,
    "audit.retention.days": 365,
  },
);
export const businessPlan = plan(
  "0198f0a2-0000-7000-8000-0000000000a3",
  "Business",
  {
    baseMonthly: 9900,
    perPersonMonthly: 800,
    baseYearly: 99000,
    perPersonYearly: 8000,
  },
  {
    "members.max": 50,
    "roles.custom": true,
    "roles.custom.max": 20,
    "audit.retention.days": 365,
  },
);
export const plans = [freePlan, teamPlan, businessPlan];

/** Team, monthly, active, paid until 1 Dec 2026. */
export const teamMonthly: SubscriptionResponse = {
  planId: teamPlan.id,
  interval: SubscriptionResponseInterval.Monthly,
  status: SubscriptionResponseStatus.Active,
  currentPeriodEnd: "2026-12-01T00:00:00Z",
  cancelAtPeriodEnd: false,
  scheduledChange: null,
  payment: null,
  priceChange: null,
};

export const billingOf = (
  subscription: SubscriptionResponse | null = teamMonthly,
  more: Partial<BillingResponse> = {},
): BillingResponse => ({
  billedBy: BillingResponseBilledBy.Card,
  subscription,
  billingEmail: "accounts@northwind.example",
  ...more,
});

/** Team's entitlements and Northwind's counts: 12 of 15 seats, 4 custom roles. */
export const teamTenant = {
  planName: "Team",
  entitlements: {
    "members.max": 15,
    "roles.custom": true,
    "roles.custom.max": 5,
    "audit.retention.days": 365,
  },
  usage: { "members.max": 12, "roles.custom.max": 4 },
};

/**
 * GetBilling, GetPlanChoices and ListMembers: eleven members, Grace among them, the Billing manager, whom ListMembers answers
 * with her role's filter.
 */
export const billingAnswers = (billing: BillingResponse = billingOf()) => [
  http.get(`*/v1/tenants/${northwind.id}/billing`, () =>
    HttpResponse.json(billing),
  ),
  http.get(`*/v1/tenants/${northwind.id}/plans`, () =>
    HttpResponse.json({ plans }),
  ),
  http.get(`*/v1/tenants/${northwind.id}/members`, ({ request }) => {
    const managers =
      new URL(request.url).searchParams.get("roleId") ===
      roleOf.billing_manager.id;
    return HttpResponse.json({
      items: managers ? [grace] : people,
      page: 1,
      pageSize: managers ? 5 : 1,
      total: managers ? 1 : 11,
    });
  }),
];
