"use client";

import { Permission } from "@repo/api-client";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { cn } from "@repo/ui/lib/utils";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { useTenant } from "@/shared/tenant-context";
import { lockOf, permissionGroups } from "./permissions";

type Editable = {
  /** The viewer's own permissions: a permission they lack is disabled (F51). */
  viewer: readonly Permission[];
  /** Checks or unchecks permissions together: one, or each that an area's "Select all" may change. */
  onChange: (permissions: readonly Permission[], checked: boolean) => void;
};

/** Why a box cannot change, or null: a lock of the editor's rules, Manage Owners in a custom role, or a permission the viewer lacks. */
const stateOf = (
  permission: Permission,
  held: readonly Permission[],
  editable: Editable | undefined,
) => {
  if (!editable) return { fixed: true, reason: null } as const;
  const lock = lockOf(permission, held);
  if (lock) return { fixed: true, reason: lock } as const;
  // Only the Owner role holds it (K1), so a custom role shows it unchecked and closed (9.08).
  if (permission === Permission.TenantOwnersManage)
    return { fixed: true, reason: "ownerOnly" } as const;
  if (!editable.viewer.includes(permission))
    return { fixed: true, reason: "lacks" } as const;
  return { fixed: false, reason: null } as const;
};

/**
 * A role's permissions in one card of areas (9.08, 9.10, 9.11): each area says how many of its permissions the role holds, and an
 * editable one offers "Select all" for the permissions the viewer may change. Each permission has its label `permissions.*` and
 * the line under it: what it gives (`roles.hints.*`), or why it cannot change. Editable, a permission is locked on by the editor's
 * rules, closed when the viewer's role lacks it, and Manage Owners shows closed. Read-only, every box is disabled, and Manage
 * Owners shows only for a role that holds it.
 */
export function PermissionList({
  held,
  editable,
}: {
  held: readonly Permission[];
  editable?: Editable;
}) {
  const t = useTranslations();
  const groups = permissionGroups.map((group) => ({
    ...group,
    permissions: group.permissions.filter(
      (permission) =>
        permission !== Permission.TenantOwnersManage ||
        editable !== undefined ||
        held.includes(permission),
    ),
  }));
  return (
    <div className="min-w-0 overflow-hidden rounded-lg border border-border bg-card">
      {groups.map((group) => {
        const area = t(`roles.groups.${group.id}`);
        const count = group.permissions.filter((permission) =>
          held.includes(permission),
        ).length;
        const free = group.permissions.filter(
          (permission) => !stateOf(permission, held, editable).fixed,
        );
        return (
          <fieldset
            key={group.id}
            className="min-w-0 border-t border-border first:border-t-0"
          >
            {/* A floated legend sits inside the card, as the frame's area row does. */}
            <legend className="float-start flex w-full items-center justify-between gap-3 bg-muted px-5 py-3">
              <span className="text-body font-medium">{area}</span>
              <span className="flex items-center gap-2 text-small text-muted-foreground">
                {t("roles.areaCount", {
                  held: count,
                  total: group.permissions.length,
                })}
                {editable && free.length > 0 && (
                  <SelectAll
                    area={area}
                    checked={free.every((permission) =>
                      held.includes(permission),
                    )}
                    onChange={(checked) => editable.onChange(free, checked)}
                  />
                )}
              </span>
            </legend>
            <div className="clear-both">
              {group.permissions.map((permission) => (
                <PermissionRow
                  key={permission}
                  permission={permission}
                  held={held}
                  editable={editable}
                />
              ))}
            </div>
          </fieldset>
        );
      })}
    </div>
  );
}

function SelectAll({
  area,
  checked,
  onChange,
}: {
  area: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  const t = useTranslations("roles");
  const id = useId();
  return (
    <>
      <span aria-hidden="true">·</span>
      <Checkbox
        id={id}
        checked={checked}
        aria-label={t("selectAllOf", { area })}
        onCheckedChange={onChange}
      />
      <label htmlFor={id} aria-hidden="true" className="text-foreground">
        {t("selectAll")}
      </label>
    </>
  );
}

function PermissionRow({
  permission,
  held,
  editable,
}: {
  permission: Permission;
  held: readonly Permission[];
  editable?: Editable;
}) {
  const t = useTranslations();
  const { detail } = useTenant();
  const id = useId();
  const lineId = `${id}-line`;
  const { fixed, reason } = stateOf(permission, held, editable);
  const lacks = reason === "lacks";
  const key = `permissions.${permission}` as "permissions.tenant.audit.read";
  const hintKey =
    `roles.hints.${permission}` as "roles.hints.tenant.audit.read";
  const line = (() => {
    if (lacks) return t("roles.lacks");
    if (t.has(hintKey)) return t(hintKey, { organisation: detail.name });
    return null;
  })();
  return (
    <div className="flex min-h-11 items-start gap-4 border-t border-border ps-6 pe-5 py-2.5">
      <Checkbox
        id={id}
        className="mt-0.5"
        checked={
          held.includes(permission) ||
          reason === "everyRole" ||
          reason === "rolesReadNeeded"
        }
        disabled={fixed}
        aria-describedby={line ? lineId : undefined}
        onCheckedChange={(checked) => editable?.onChange([permission], checked)}
      />
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <label
          htmlFor={id}
          className={cn(
            "text-body",
            (lacks || reason === "ownerOnly") && "text-muted-foreground",
          )}
        >
          {t.has(key) ? t(key) : <bdi dir="ltr">{permission}</bdi>}
        </label>
        {line && (
          <p
            id={lineId}
            className={cn(
              "text-small",
              lacks ? "text-destructive-text" : "text-muted-foreground",
            )}
          >
            {line}
          </p>
        )}
      </div>
    </div>
  );
}
