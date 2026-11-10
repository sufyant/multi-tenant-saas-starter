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
 * The members of an organisation as the Overview and the Plan tab say them (4.09, 4.14): "11 members and 2 pending invitations: 12
 * of 15 seats." The API counts each number (`memberCount`, `pendingInvitationCount`, the seats' `count`); the screen adds nothing.
 * Without a seat limit the sentence names only the counts. The billing manager clause is step 40a.
 */
export function useMembersText() {
  const t = useTranslations("organisations");
  return (
    organisation: Pick<
      TenantDetailResponse,
      "memberCount" | "pendingInvitationCount"
    >,
    plan: TenantPlanResponse | undefined,
  ) => {
    const members = t("memberCount", { count: organisation.memberCount });
    const counts =
      organisation.pendingInvitationCount > 0
        ? t("membersAndPending", {
            members,
            count: organisation.pendingInvitationCount,
          })
        : members;
    const seats = seatsOf(plan);
    return {
      sentence: seats ? t("membersOfSeats", { counts, ...seats }) : counts,
      seats: seats && t("seatsUsed", seats),
    };
  };
}
