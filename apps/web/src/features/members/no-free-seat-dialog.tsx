"use client";

import { LimitKey } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Dialog } from "@repo/ui/components/dialog";
import { useTranslations } from "next-intl";
import { usePlanChange } from "@/shared/billing";
import { usePlan } from "@/shared/plan";
import { useTenant } from "@/shared/tenant-context";
import { sentenceOf, usePlanReason } from "@/shared/use-plan-reason";

/**
 * "No free seat" (8.39 and 8.43 at the limit, 8.40 and 8.45 over it): "Invite member" stays enabled at and over the limit and
 * opens this dialog, which says the seats, how they split, the Billing managers apart, and what to do (F56). A viewer who manages
 * billing of an organisation that pays by card gets Change plan (8.39, 8.40); otherwise its one way to support is "Request a plan
 * change", the brand's support contact (8.43, 8.45). "View invitations" opens the Invitations tab for whom may read it. Until the
 * members' total has come, and at a limit of 0, the dialog says the limit's reason without the split.
 */
export function NoFreeSeatDialog({
  members,
  billing,
  onViewInvitations,
  onClose,
}: {
  /** ListMembers' `total` without a filter, or undefined while it loads. */
  members: number | undefined;
  /** The Billing managers among the members, who take no seat (A2). */
  billing: number;
  /** Absent for a viewer who may not read the invitations. */
  onViewInvitations?: () => void;
  onClose: () => void;
}) {
  const t = useTranslations("plans");
  const { detail } = useTenant();
  const plan = usePlan();
  const planReason = usePlanReason();
  const way = usePlanChange();
  const limit = plan.limit(LimitKey.MembersMax);
  const more =
    way === "change"
      ? {
          label: t("changePlan"),
          go: () =>
            window.location.assign(`/${detail.slug}/settings/billing/plans`),
        }
      : {
          label: t("requestChange"),
          go: () => window.open(brand.supportUrl, "_self"),
        };

  const text = (() => {
    // A limit of 0 has no seat to free, so the dialog says only the limit's reason.
    if (members === undefined || limit.limit === null || limit.limit === 0)
      return sentenceOf(planReason.limit(LimitKey.MembersMax));
    const values = {
      ...plan.seats(LimitKey.MembersMax, members, billing),
      organisation: detail.name,
      count: limit.count,
      limit: limit.limit,
      change: way === "change" ? "yes" : "no",
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
        onViewInvitations ? { label: more.label, onBack: more.go } : undefined
      }
      actionLabel={onViewInvitations ? t("viewInvitations") : more.label}
      onConfirm={onViewInvitations ?? more.go}
    />
  );
}
