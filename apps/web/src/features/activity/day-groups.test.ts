import type { AuditEntryResponse } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { groupByDay } from "./day-groups";

const at = (occurredAt: string) => ({ occurredAt }) as AuditEntryResponse;

// Newest first, as the API answers: 23:30 and 22:30 UTC on the 15th, then 20:30 UTC.
const entries = [
  at("2026-10-15T23:30:00Z"),
  at("2026-10-15T22:30:00Z"),
  at("2026-10-15T20:30:00Z"),
];

const days = (timeZone: string) =>
  groupByDay(entries, timeZone).map((group) => [
    group.day,
    group.entries.map((entry) => entry.occurredAt),
  ]);

describe("groupByDay", () => {
  it("GroupByDay_Dublin_SplitsAtDublinsMidnight", () => {
    expect(days("Europe/Dublin")).toEqual([
      ["2026-10-16", ["2026-10-15T23:30:00Z"]],
      ["2026-10-15", ["2026-10-15T22:30:00Z", "2026-10-15T20:30:00Z"]],
    ]);
  });

  it("GroupByDay_Riyadh_SplitsAtRiyadhsMidnight", () => {
    expect(days("Asia/Riyadh")).toEqual([
      ["2026-10-16", ["2026-10-15T23:30:00Z", "2026-10-15T22:30:00Z"]],
      ["2026-10-15", ["2026-10-15T20:30:00Z"]],
    ]);
  });

  it("GroupByDay_NoEntries_IsEmpty", () => {
    expect(groupByDay([], "Europe/Dublin")).toEqual([]);
  });
});
