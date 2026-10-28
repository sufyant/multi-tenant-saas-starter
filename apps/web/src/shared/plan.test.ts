import { describe, expect, it } from "vitest";
import { free, planWith, pro } from "../../test/tenant";
import { EntitlementKey, featureOn, limitOf } from "./plan";

describe("featureOn", () => {
  it("FeatureOn_FeatureTrue_On", () => {
    expect(featureOn(pro, EntitlementKey.RolesCustom)).toBe(true);
  });

  it("FeatureOn_FeatureFalse_Off", () => {
    expect(featureOn(free, EntitlementKey.RolesCustom)).toBe(false);
  });

  it("FeatureOn_MissingKey_Off", () => {
    expect(
      featureOn({ entitlements: {}, usage: {} }, EntitlementKey.RolesCustom),
    ).toBe(false);
  });
});

describe("limitOf", () => {
  it("LimitOf_UnderTheLimit_CountsWithRoomLeft", () => {
    const plan = planWith({ "members.max": 15 }, { "members.max": 10 });

    expect(limitOf(plan, EntitlementKey.MembersMax)).toEqual({
      limit: 15,
      count: 10,
      atLimit: false,
      overLimit: false,
      full: false,
    });
  });

  it("LimitOf_AtTheLimit_Full", () => {
    const plan = planWith({ "members.max": 10 }, { "members.max": 10 });

    expect(limitOf(plan, EntitlementKey.MembersMax)).toEqual({
      limit: 10,
      count: 10,
      atLimit: true,
      overLimit: false,
      full: true,
    });
  });

  it("LimitOf_OverTheLimit_FullAndOver", () => {
    const plan = planWith({ "members.max": 5 }, { "members.max": 10 });

    expect(limitOf(plan, EntitlementKey.MembersMax)).toEqual({
      limit: 5,
      count: 10,
      atLimit: false,
      overLimit: true,
      full: true,
    });
  });

  it("LimitOf_Unlimited_NeverFull", () => {
    const plan = planWith({ "members.max": null }, { "members.max": 900 });

    expect(limitOf(plan, EntitlementKey.MembersMax)).toEqual({
      limit: null,
      count: 900,
      atLimit: false,
      overLimit: false,
      full: false,
    });
  });

  it("LimitOf_MissingKey_ClosedAtZero", () => {
    expect(
      limitOf({ entitlements: {}, usage: {} }, EntitlementKey.RolesCustomMax),
    ).toEqual({
      limit: 0,
      count: 0,
      atLimit: true,
      overLimit: false,
      full: true,
    });
  });
});
