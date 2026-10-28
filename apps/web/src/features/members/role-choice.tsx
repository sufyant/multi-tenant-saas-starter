"use client";

import type { RoleDetailResponse } from "@repo/api-client";
import { RadioGroup, RadioGroupItem } from "@repo/ui/components/ui/radio-group";
import { cn } from "@repo/ui/lib/utils";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { EntitlementKey, usePlan } from "@/shared/plan";
import { inListOrder } from "@/shared/roles";
import { usePlanReason } from "@/shared/use-plan-reason";
import { useRoleDescription, useRoleName } from "@/shared/use-role-name";
import { useRoleReach } from "./use-role-reach";

/**
 * The role list of the ChangeRole and Invitations boards: every role of the organisation in list order, one radio each, with its
 * description or its permission count. A role the viewer cannot give stays, disabled, with its reason under it (F51). With
 * `roles.custom` off, a custom role stays, disabled, with the plan's reason; a built-in role is always given (the plan-limit
 * boards). A long list scrolls, so the dialog's buttons stay in view.
 */
export function RoleChoice({
  roles,
  value,
  onChange,
  ownerReason,
  label,
}: {
  roles: readonly RoleDetailResponse[];
  /** The id of the chosen role. */
  value: string;
  onChange: (roleId: string) => void;
  /** Why the viewer cannot give the Owner role: the K1 text of this dialog. */
  ownerReason: string;
  /** A visible label above the list; without it the legend is for screen readers only. */
  label?: string;
}) {
  const t = useTranslations();
  const roleName = useRoleName();
  const roleDescription = useRoleDescription();
  const reach = useRoleReach();
  const plan = usePlan();
  const planReason = usePlanReason();
  const featureOff = plan.on(EntitlementKey.RolesCustom)
    ? null
    : planReason.feature(EntitlementKey.RolesCustom);
  const id = useId();
  return (
    <fieldset className="flex min-w-0 flex-col gap-1.5">
      <legend
        id={`${id}-legend`}
        className={cn(label ? "mb-1.5 text-body font-medium" : "sr-only")}
      >
        {label ?? t("members.role")}
      </legend>
      <RadioGroup
        name={`${id}-role`}
        value={value}
        onValueChange={(roleId) => onChange(String(roleId))}
        aria-labelledby={`${id}-legend`}
        className="flex max-h-85 flex-col gap-2 overflow-y-auto p-0.5 max-sm:max-h-65"
      >
        {inListOrder(roles).map((role) => {
          const reason =
            reach(role.id, ownerReason) ?? (role.builtIn ? null : featureOff);
          const off = reason !== null;
          return (
            <label
              key={role.id}
              htmlFor={`${id}-${role.id}-item`}
              className={cn(
                "flex items-start gap-2.5 rounded-md border border-border px-3 py-2.5",
                off ? "cursor-not-allowed" : "cursor-pointer",
                role.id === value && "border-primary ring-1 ring-primary",
              )}
            >
              <RadioGroupItem
                id={`${id}-${role.id}-item`}
                value={role.id}
                disabled={off}
                aria-labelledby={`${id}-${role.id}-name`}
                aria-describedby={cn(
                  `${id}-${role.id}`,
                  reason && `${id}-${role.id}-reason`,
                )}
                className="mt-0.5"
              />
              <span className="flex min-w-0 flex-col gap-0.5">
                <span
                  id={`${id}-${role.id}-name`}
                  className={cn("font-medium", off && "text-muted-foreground")}
                >
                  <bdi>{roleName(role)}</bdi>
                </span>
                <span
                  id={`${id}-${role.id}`}
                  className="text-small text-muted-foreground"
                >
                  {roleDescription(role)}
                </span>
                {reason && (
                  <span
                    id={`${id}-${role.id}-reason`}
                    className="text-small text-destructive-text"
                  >
                    {reason}
                  </span>
                )}
              </span>
            </label>
          );
        })}
      </RadioGroup>
    </fieldset>
  );
}
