"use client";

import {
  LimitKey,
  type TenantDetailResponse,
  type TenantPlanResponse,
} from "@repo/api-client";
import { useTranslations } from "next-intl";

/** The seats of a plan with a number: the seats the members and the pending invitations take, of the limit (4.09). */
export function seatsOf(plan: TenantPlanResponse | undefined) {
  const seats = plan?.entitlements.find(
    (row) => row.key === LimitKey.MembersMax,
  );
  const limit = seats?.effective.maximum;
  return seats && !seats.effective.unlimited && limit != null
    ? { used: seats.count, limit }
    : null;
}

/**
 * The members of an organisation as the Overview and the Plan tab say them (4.09, 4.14, 4.27): "11 members, 1 of them a billing
 * manager who uses no seat, and 2 pending invitations: 12 of 15 seats." The API counts each number (`memberCount`,
 * `freeMemberCount`, `pendingInvitationCount`, the seats' `count`); the screen adds nothing. Without a seat limit the sentence names
 * only the counts.
 */
export function useMembersText() {
  const t = useTranslations("organisations");
  return (
    organisation: Pick<
      TenantDetailResponse,
      "memberCount" | "freeMemberCount" | "pendingInvitationCount"
    >,
    plan: TenantPlanResponse | undefined,
  ) => {
    const count = t("memberCount", { count: organisation.memberCount });
    const free = organisation.freeMemberCount > 0;
    const members = free
      ? t("membersWithFree", {
          members: count,
          count: organisation.freeMemberCount,
        })
      : count;
    const pending = organisation.pendingInvitationCount;
    const counts =
      pending > 0
        ? t(free ? "membersFreeAndPending" : "membersAndPending", {
            members,
            count: pending,
          })
        : members;
    const seats = seatsOf(plan);
    return {
      sentence: seats ? t("membersOfSeats", { counts, ...seats }) : counts,
      seats: seats && t("seatsUsed", seats),
    };
  };
}
