"use client";

import { LimitKey } from "@repo/api-client";
import { brand } from "@repo/brand";
import { Dialog } from "@repo/ui/components/dialog";
import { useTranslations } from "next-intl";
import { usePlan } from "@/shared/plan";
import { useTenant } from "@/shared/tenant-context";
import { sentenceOf, usePlanReason } from "@/shared/use-plan-reason";

/**
 * "No custom role left": "New role" stays enabled at and over the limit of `roles.custom.max` and opens this dialog, which says
 * the limit, the organisation's count and what to do (F56, as Members' "No free seat"). Without billing its one way to support
 * is "Request a plan change", the brand's support contact; 40b's Change plan takes its place with billing on (9.14). At a limit
 * of 0 it says only the limit's reason, as there is no role to delete.
 */
export function NoRoleLeftDialog({ onClose }: { onClose: () => void }) {
  const t = useTranslations();
  const { detail } = useTenant();
  const planReason = usePlanReason();
  const limit = usePlan().limit(LimitKey.RolesCustomMax);
  const text =
    limit.limit === null || limit.limit === 0
      ? sentenceOf(planReason.limit(LimitKey.RolesCustomMax))
      : t("plans.noRoleLeftText", {
          limit: limit.limit,
          organisation: detail.name,
          count: limit.count,
        });
  return (
    <Dialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={t("plans.noRoleLeft")}
      description={text}
      cancelLabel={t("common.close")}
      actionLabel={t("plans.requestChange")}
      onConfirm={() => window.open(brand.supportUrl, "_self")}
    />
  );
}
