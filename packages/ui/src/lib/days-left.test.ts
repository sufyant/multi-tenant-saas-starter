import { describe, expect, it } from "vitest";
import { daysLeft } from "./days-left";

const now = Date.parse("2026-11-02T10:00:00Z");

describe("daysLeft", () => {
  it("DaysLeft_WholeDays_CountsThem", () => {
    expect(daysLeft("2026-11-05T10:00:00Z", now)).toBe(3);
  });

  it("DaysLeft_PartOfADay_CountsAsADay", () => {
    expect(daysLeft("2026-11-02T11:00:00Z", now)).toBe(1);
  });

  it("DaysLeft_Past_IsZero", () => {
    expect(daysLeft("2026-11-01T10:00:00Z", now)).toBe(0);
  });
});
