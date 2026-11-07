import { FeatureKey, LimitKey } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import { free, planWith, pro } from "../../test/tenant";
import { featureOn, limitOf, seatsOf } from "./plan";

describe("featureOn", () => {
  it("FeatureOn_FeatureTrue_On", () => {
    expect(featureOn(pro, FeatureKey.RolesCustom)).toBe(true);
  });

  it("FeatureOn_FeatureFalse_Off", () => {
    expect(featureOn(free, FeatureKey.RolesCustom)).toBe(false);
  });

  it("FeatureOn_MissingKey_Off", () => {
    expect(
      featureOn({ entitlements: {}, usage: {} }, FeatureKey.RolesCustom),
    ).toBe(false);
  });
});

describe("limitOf", () => {
  it("LimitOf_UnderTheLimit_CountsWithRoomLeft", () => {
    const plan = planWith({ "members.max": 15 }, { "members.max": 10 });

    expect(limitOf(plan, LimitKey.MembersMax)).toEqual({
      limit: 15,
      count: 10,
      left: 5,
      atLimit: false,
      overLimit: false,
      full: false,
    });
  });

  it("LimitOf_AtTheLimit_Full", () => {
    const plan = planWith({ "members.max": 10 }, { "members.max": 10 });

    expect(limitOf(plan, LimitKey.MembersMax)).toEqual({
      limit: 10,
      count: 10,
      left: 0,
      atLimit: true,
      overLimit: false,
      full: true,
    });
  });

  it("LimitOf_OverTheLimit_FullAndOver", () => {
    const plan = planWith({ "members.max": 5 }, { "members.max": 10 });

    expect(limitOf(plan, LimitKey.MembersMax)).toEqual({
      limit: 5,
      count: 10,
      left: 0,
      atLimit: false,
      overLimit: true,
      full: true,
    });
  });

  it("LimitOf_Unlimited_NeverFull", () => {
    const plan = planWith({ "members.max": null }, { "members.max": 900 });

    expect(limitOf(plan, LimitKey.MembersMax)).toEqual({
      limit: null,
      count: 900,
      left: null,
      atLimit: false,
      overLimit: false,
      full: false,
    });
  });

  it("LimitOf_MissingKey_ClosedAtZero", () => {
    expect(
      limitOf({ entitlements: {}, usage: {} }, LimitKey.RolesCustomMax),
    ).toEqual({
      limit: 0,
      count: 0,
      left: 0,
      atLimit: true,
      overLimit: false,
      full: true,
    });
  });
});

describe("seatsOf", () => {
  it("SeatsOf_MembersUnderTheCount_TheRestArePendingInvitations", () => {
    const plan = planWith({ "members.max": 15 }, { "members.max": 17 });

    expect(seatsOf(limitOf(plan, LimitKey.MembersMax), 15)).toEqual({
      members: 15,
      invitations: 2,
    });
  });

  it("SeatsOf_MoreMembersThanTheCount_NoInvitationsBelowZero", () => {
    const plan = planWith({ "members.max": 15 }, { "members.max": 14 });

    expect(seatsOf(limitOf(plan, LimitKey.MembersMax), 16)).toEqual({
      members: 16,
      invitations: 0,
    });
  });
});
