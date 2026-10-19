import {
  type InvitationResponse,
  InvitationResponseState,
} from "@repo/api-client";

const day = 24 * 60 * 60 * 1000;

/**
 * Under a day left, a pending invitation shows "Expires soon" in the warning tone (the Invitations board). The API's state stays
 * `pending`: the threshold is the screen's own, so a person has a day to ask for a new link before the old one stops.
 */
export const expiresSoonWithin = day;

export type InvitationStatus = "invited" | "expiresSoon" | "expired";

/** The status a row shows: Expired from the API's state, else Expires soon or Invited from the time left. */
export function invitationStatus(
  invitation: Pick<InvitationResponse, "state" | "expiresAt">,
  now: Date,
): InvitationStatus {
  if (invitation.state === InvitationResponseState.Expired) return "expired";
  const left = new Date(invitation.expiresAt).getTime() - now.getTime();
  return left < expiresSoonWithin ? "expiresSoon" : "invited";
}

/** Whole days from now until the expiry the API answered, for "has 7 days to join": never a literal. */
export function daysLeft(expiresAt: string, now: Date): number {
  return Math.round((new Date(expiresAt).getTime() - now.getTime()) / day);
}
