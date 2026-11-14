"use client";

import { LimitKey } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Dialog } from "@repo/ui/components/dialog";
import { useTranslations } from "next-intl";
import { usePlan } from "@/shared/plan";
import { useTenant } from "@/shared/tenant-context";
import { sentenceOf, usePlanReason } from "@/shared/use-plan-reason";

/**
 * "No free seat" (8.43 at the limit, 8.45 over it): "Invite member" stays enabled at and over the limit and opens this dialog,
 * which says the seats, how they split, and what to do (F56). Its one way to support is "Request a plan change", the brand's
 * support contact; "View invitations" opens the Invitations tab for whom may read it. Until the members' total has come, and at a
 * limit of 0, the dialog says the limit's reason without the split.
 */
export function NoFreeSeatDialog({
  members,
  onViewInvitations,
  onClose,
}: {
  /** ListMembers' `total` without a filter, or undefined while it loads. */
  members: number | undefined;
  /** Absent for a viewer who may not read the invitations. */
  onViewInvitations?: () => void;
  onClose: () => void;
}) {
  const t = useTranslations("plans");
  const { detail } = useTenant();
  const plan = usePlan();
  const planReason = usePlanReason();
  const limit = plan.limit(LimitKey.MembersMax);
  const requestChange = () => window.open(brand.supportUrl, "_self");

  const text = (() => {
    // A limit of 0 has no seat to free, so the dialog says only the limit's reason.
    if (members === undefined || limit.limit === null || limit.limit === 0)
      return sentenceOf(planReason.limit(LimitKey.MembersMax));
    const values = {
      ...plan.seats(LimitKey.MembersMax, members),
      organisation: detail.name,
      count: limit.count,
      limit: limit.limit,
    };
    return limit.overLimit
      ? t("noSeatOverText", values)
      : t("noSeatText", values);
  })();

  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("noSeat")}
      description={text}
      // The Dialog of packages/ui has one second button, in the place of Back (37a-shared-components draws the footer again).
      back={
        onViewInvitations
          ? { label: t("requestChange"), onBack: requestChange }
          : undefined
      }
      actionLabel={
        onViewInvitations ? t("viewInvitations") : t("requestChange")
      }
      onConfirm={onViewInvitations ?? requestChange}
    />
  );
}
