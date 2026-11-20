import type { UsageResponse } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { pastMonthsOf, usageFormat } from "./usage-text";

const usage = {
  periodStart: "2026-11-01",
  periodEnd: "2026-11-30",
  planName: "Team",
  currency: "EUR",
  meters: [],
  pastPeriods: [
    {
      periodStart: "2026-10-01",
      meters: [
        { key: "ai.requests", used: 10600, extra: 600, extraAmount: 60 },
        { key: "storage.gb", used: 12, extra: 2, extraAmount: 200 },
      ],
    },
  ],
  spendingCap: null,
  spendingCapOffered: true,
} satisfies UsageResponse;

describe("usageFormat", () => {
  it("UsageFormat_TheMonth_ReadsItsDaysInUtcAndResetsTheDayAfterItsEnd", () => {
    const text = usageFormat(usage, "en-IE");

    expect(text.resetsOn).toBe("1 Dec");
    expect(text.day("2026-11-24T23:30:00Z")).toBe("24 Nov");
    expect(text.date("2026-11-30")).toBe("30 Nov 2026");
    expect(text.month("2026-10-01")).toBe("October 2026");
    expect(text.count(10600)).toBe("10,600");
    expect(text.money(5000)).toBe("€50");
  });
});

describe("pastMonthsOf", () => {
  it("PastMonthsOf_AMonthOfTwoMeters_GivesEachMetersUseAndAddsTheExtra", () => {
    expect(pastMonthsOf(usage)).toEqual({
      keys: ["ai.requests", "storage.gb"],
      rows: [
        {
          periodStart: "2026-10-01",
          meters: { "ai.requests": 10600, "storage.gb": 12 },
          extra: 602,
          extraAmount: 260,
        },
      ],
    });
  });
});
