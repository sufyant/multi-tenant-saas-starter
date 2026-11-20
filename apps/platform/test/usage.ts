import {
  MeterSettingResponseOverage,
  type MeterUsageResponse,
  type UsageResponse,
} from "@repo/api-client";
import { HttpResponse, http } from "msw";

// An organisation's usage as the platform's Usage frames draw it (4.37): Team gives 10,000 of the meter "ai.requests" a month, and the
// extra costs €1 per 1,000. The template ships no word for a meter, so the screens name it by its key.

/** The meter of Team this month: 7,200 of 10,000 (4.37). */
export const teamMeter = (
  more: Partial<MeterUsageResponse> = {},
): MeterUsageResponse => ({
  key: "ai.requests",
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

/** The meter of Free this month, a meter that stops: all 100 used on 18 Nov (4.42). */
export const freeMeter: MeterUsageResponse = {
  key: "ai.requests",
  quota: 100,
  overage: MeterSettingResponseOverage.Stop,
  unitPrice: 0,
  unitSize: 1,
  used: 100,
  extra: 0,
  extraAmount: 0,
  quotaReachedAt: "2026-11-18T10:00:00Z",
};

/** November 2026 on Team, with the three months before it (4.37). */
export const usageOf = (more: Partial<UsageResponse> = {}): UsageResponse => ({
  periodStart: "2026-11-01",
  periodEnd: "2026-11-30",
  planName: "Team",
  currency: "EUR",
  meters: [teamMeter()],
  pastPeriods: [
    {
      periodStart: "2026-10-01",
      meters: [{ key: "ai.requests", used: 9100, extra: 0, extraAmount: 0 }],
    },
    {
      periodStart: "2026-09-01",
      meters: [
        { key: "ai.requests", used: 10600, extra: 600, extraAmount: 60 },
      ],
    },
    {
      periodStart: "2026-08-01",
      meters: [{ key: "ai.requests", used: 8300, extra: 0, extraAmount: 0 }],
    },
  ],
  spendingCap: null,
  spendingCapOffered: true,
  ...more,
});

/** GetTenantUsage of any organisation. */
export const usageAnswer = (usage: UsageResponse = usageOf()) =>
  http.get("*/v1/platform/tenants/:tenantId/usage", () =>
    HttpResponse.json(usage),
  );

/** GetTenantUsage of a plan without a meter, as the template ships it. */
export const noMeterAnswer = () =>
  usageAnswer(usageOf({ meters: [], pastPeriods: [] }));
