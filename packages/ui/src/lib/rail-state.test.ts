import { describe, expect, it } from "vitest";
import { railCookieOf, railExpandedFrom } from "./rail-state";

describe("rail-state", () => {
  it("RailExpandedFrom_True_IsExpanded", () => {
    expect(railExpandedFrom("true")).toBe(true);
  });

  it("RailExpandedFrom_NoCookie_IsTheIconRail", () => {
    expect(railExpandedFrom(undefined)).toBe(false);
  });

  it("RailExpandedFrom_AnotherValue_IsTheIconRail", () => {
    expect(railExpandedFrom("yes")).toBe(false);
  });

  it("RailCookieOf_Expanded_NamesTheCookieForTheWholeOriginForSevenDays", () => {
    expect(railCookieOf(true)).toBe(
      "sidebar_state=true; path=/; max-age=604800; samesite=lax",
    );
  });
});
