import {
  MeterSettingResponseOverage,
  type MeterUsageResponse,
  type PlanChoiceResponse,
  type UsageResponse,
} from "@repo/api-client";
import { HttpResponse, http } from "msw";
import { freePlan, plans, teamPlan } from "./billing";
import { northwind } from "./tenant";

// Northwind's usage as the Usage frames draw it (11.01): Team gives 10,000 of the meter "ai.requests" a month, and the extra costs
// €1 per 1,000. The template ships no word for a meter, so the screens name it by its key.

export const meterKey = "ai.requests";

/** The meter of Team this month: 7,200 of 10,000 (11.01). */
export const teamMeter = (
  more: Partial<MeterUsageResponse> = {},
): MeterUsageResponse => ({
  key: meterKey,
  quota: 10000,
  overage: MeterSettingResponseOverage.Charge,
  unitPrice: 100,
  unitSize: 1000,
  used: 7200,
  extra: 0,
  extraAmount: 0,
  quotaReachedAt: null,
  ...more,
});

/** The meter of Free this month, a meter that stops: all 100 used (11.05). */
export const freeMeter: MeterUsageResponse = {
  key: meterKey,
  quota: 100,
  overage: MeterSettingResponseOverage.Stop,
  unitPrice: 0,
  unitSize: 1,
  used: 100,
  extra: 0,
  extraAmount: 0,
  quotaReachedAt: "2026-11-20T10:00:00Z",
};

/** November 2026 on Team, with the three months before it (11.01). */
export const usageOf = (more: Partial<UsageResponse> = {}): UsageResponse => ({
  periodStart: "2026-11-01",
  periodEnd: "2026-11-30",
  planName: "Team",
  currency: "EUR",
  meters: [teamMeter()],
  pastPeriods: [
    {
      periodStart: "2026-10-01",
      meters: [{ key: meterKey, used: 9100, extra: 0, extraAmount: 0 }],
    },
    {
      periodStart: "2026-09-01",
      meters: [{ key: meterKey, used: 10600, extra: 600, extraAmount: 60 }],
    },
    {
      periodStart: "2026-08-01",
      meters: [{ key: meterKey, used: 8300, extra: 0, extraAmount: 0 }],
    },
  ],
  spendingCap: null,
  spendingCapOffered: true,
  ...more,
});

/** The plans with their meters: Free stops at 100, Team gives 10,000 and charges the extra. */
export const meteredPlans: PlanChoiceResponse[] = plans.map((plan) => ({
  ...plan,
  meters:
    plan.id === freePlan.id
      ? [
          {
            key: meterKey,
            quota: 100,
            overage: MeterSettingResponseOverage.Stop,
            unitPrice: 0,
            unitSize: 1,
          },
        ]
      : plan.id === teamPlan.id
        ? [
            {
              key: meterKey,
              quota: 10000,
              overage: MeterSettingResponseOverage.Charge,
              unitPrice: 100,
              unitSize: 1000,
            },
          ]
        : [
            {
              key: meterKey,
              quota: 50000,
              overage: MeterSettingResponseOverage.Charge,
              unitPrice: 80,
              unitSize: 1000,
            },
          ],
}));

/** GetUsage and GetPlanChoices of Northwind. */
export const usageAnswers = (usage: UsageResponse = usageOf()) => [
  http.get(`*/v1/tenants/${northwind.id}/usage`, () =>
    HttpResponse.json(usage),
  ),
  http.get(`*/v1/tenants/${northwind.id}/plans`, () =>
    HttpResponse.json({ plans: meteredPlans }),
  ),
];
