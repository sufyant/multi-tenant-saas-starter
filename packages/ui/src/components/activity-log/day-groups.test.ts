import { describe, expect, it } from "vitest";
import { groupByDay } from "./day-groups";

// Newest first, as the API answers: 23:30 and 22:30 UTC on the 15th, then 20:30 UTC.
const entries = [
  { id: "1", occurredAt: "2026-10-15T23:30:00Z" },
  { id: "2", occurredAt: "2026-10-15T22:30:00Z" },
  { id: "3", occurredAt: "2026-10-15T20:30:00Z" },
];

const days = (timeZone: string) =>
  groupByDay(entries, timeZone).map((group) => [
    group.day,
    group.entries.map((entry) => entry.id),
  ]);

describe("groupByDay", () => {
  it("GroupByDay_Dublin_SplitsAtDublinsMidnight", () => {
    expect(days("Europe/Dublin")).toEqual([
      ["2026-10-16", ["1"]],
      ["2026-10-15", ["2", "3"]],
    ]);
  });

  it("GroupByDay_Riyadh_SplitsAtRiyadhsMidnight", () => {
    expect(days("Asia/Riyadh")).toEqual([
      ["2026-10-16", ["1", "2"]],
      ["2026-10-15", ["3"]],
    ]);
  });

  it("GroupByDay_NoEntries_IsEmpty", () => {
    expect(groupByDay([], "Europe/Dublin")).toEqual([]);
  });
});
