"use client";

import type { RoleDetailResponse } from "@repo/api-client";
import { cn } from "@repo/ui/lib/utils";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { inListOrder } from "@/shared/roles";
import { useRoleDescription, useRoleName } from "@/shared/use-role-name";
import { useRoleReach } from "./use-role-reach";

/**
 * The role list of the ChangeRole and Invitations boards: every role of the organisation in list order, one radio each, with its
 * description or its permission count. A role the viewer cannot give stays, disabled, with its reason under it (F51). A long list
 * scrolls, so the dialog's buttons stay in view.
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
  const id = useId();
  return (
    <fieldset className="flex min-w-0 flex-col gap-1.5">
      <legend
        className={cn(label ? "mb-1.5 text-body font-medium" : "sr-only")}
      >
        {label ?? t("members.role")}
      </legend>
      <div className="flex max-h-85 flex-col gap-2 overflow-y-auto p-0.5 max-sm:max-h-65">
        {inListOrder(roles).map((role) => {
          const reason = reach(role.id, ownerReason);
          const off = reason !== null;
          return (
            <label
              key={role.id}
              className={cn(
                "flex items-start gap-2.5 rounded-md border border-border px-3 py-2.5",
                off ? "cursor-not-allowed" : "cursor-pointer",
                role.id === value && "border-primary ring-1 ring-primary",
              )}
            >
              <input
                type="radio"
                name={`${id}-role`}
                value={role.id}
                checked={role.id === value}
                disabled={off}
                onChange={() => onChange(role.id)}
                aria-labelledby={`${id}-${role.id}-name`}
                aria-describedby={cn(
                  `${id}-${role.id}`,
                  reason && `${id}-${role.id}-reason`,
                )}
                className="mt-0.5 size-4 shrink-0 accent-primary"
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
      </div>
    </fieldset>
  );
}
