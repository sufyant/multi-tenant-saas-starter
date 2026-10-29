"use client";

import { FeatureKey, LimitKey, Permission } from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { PermissionDenied } from "@/shared/permission-denied";
import { usePlan } from "@/shared/plan";
import { useCan, useTenant } from "@/shared/tenant-context";
import { type PlanReason, usePlanReason } from "@/shared/use-plan-reason";
import { RoleEditor } from "./role-editor";
import { RolesFrame } from "./roles-frame";

/**
 * A new role (the RoleCreate board), for a viewer with `tenant.roles.create` (F5) while the plan has room for it (F56): with
 * `roles.custom` off or at the limit the page says why, as "Create role" does, and offers the way back. The page decides when it
 * opens, so the save of the last role the plan allows, which reads GetMyTenant again, still leaves for the list.
 */
export function NewRolePage() {
  const t = useTranslations();
  const can = useCan();
  const { tenant } = useTenant();
  const plan = usePlan();
  const planReason = usePlanReason();
  const [closed] = useState<PlanReason | null>(() => {
    if (!plan.on(FeatureKey.RolesCustom))
      return {
        title: planReason.feature(FeatureKey.RolesCustom),
        text: null,
      };
    return plan.limit(LimitKey.RolesCustomMax).full
      ? planReason.limit(LimitKey.RolesCustomMax)
      : null;
  });

  if (!can(Permission.TenantRolesCreate))
    return (
      <RolesFrame>
        <PermissionDenied page={t("roles.newRole")} />
      </RolesFrame>
    );
  if (closed)
    return (
      <RolesFrame>
        <h1 className="text-page-title">{t("roles.newRole")}</h1>
        <Banner tone="info" title={closed.title}>
          {closed.text}
        </Banner>
        <div>
          <Button
            render={<Link href={`/${tenant.slug}/settings/roles`} />}
            nativeButton={false}
          >
            {t("roles.backToRoles")}
          </Button>
        </div>
      </RolesFrame>
    );
  return (
    <RolesFrame>
      <RoleEditor />
    </RolesFrame>
  );
}
