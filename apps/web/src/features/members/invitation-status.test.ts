import { InvitationResponseState } from "@repo/api-client";
import { describe, expect, it } from "vitest";
import {
  daysLeft,
  expiresSoonWithin,
  invitationStatus,
} from "./invitation-status";

const now = new Date("2026-10-16T13:32:00Z");
const pendingIn = (milliseconds: number) => ({
  state: InvitationResponseState.Pending,
  expiresAt: new Date(now.getTime() + milliseconds).toISOString(),
});

describe("invitationStatus", () => {
  it("InvitationStatus_ExactlyADayLeft_IsInvited", () => {
    expect(invitationStatus(pendingIn(expiresSoonWithin), now)).toBe("invited");
  });

  it("InvitationStatus_JustUnderADayLeft_ExpiresSoon", () => {
    expect(invitationStatus(pendingIn(expiresSoonWithin - 1), now)).toBe(
      "expiresSoon",
    );
  });

  it("InvitationStatus_SixDaysLeft_IsInvited", () => {
    expect(invitationStatus(pendingIn(6 * expiresSoonWithin), now)).toBe(
      "invited",
    );
  });

  it("InvitationStatus_ExpiredByTheApi_IsExpired", () => {
    expect(
      invitationStatus(
        {
          state: InvitationResponseState.Expired,
          expiresAt: now.toISOString(),
        },
        now,
      ),
    ).toBe(InvitationResponseState.Expired);
  });
});

describe("daysLeft", () => {
  it("DaysLeft_SevenDaysLessASecond_IsSeven", () => {
    expect(
      daysLeft(
        new Date(now.getTime() + 7 * expiresSoonWithin - 1000).toISOString(),
        now,
      ),
    ).toBe(7);
  });
});
