"use client";

import { Permission } from "@repo/api-client";
import { Icon, LockIcon } from "@repo/ui/components/icon";
import { cn } from "@repo/ui/lib/utils";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { lockOf, permissionGroups } from "./permissions";

type Editable = {
  /** The viewer's own permissions: a permission they lack is disabled (F51). */
  viewer: readonly Permission[];
  onChange: (permission: Permission, checked: boolean) => void;
};

/**
 * A role's permissions in their groups (the RoleCreate and RoleBuiltIn boards), each with its label `permissions.*` and its state.
 * Editable, a permission is locked on by the editor's rules or disabled when the viewer's role lacks it, each with its reason;
 * Manage Owners never shows, because no custom role holds it (K1). Read-only, every box is disabled, and Manage Owners shows only
 * for a role that holds it.
 */
export function PermissionList({
  held,
  editable,
}: {
  held: readonly Permission[];
  editable?: Editable;
}) {
  const t = useTranslations();
  const groups = permissionGroups
    .map((group) => ({
      ...group,
      permissions: group.permissions.filter(
        (permission) =>
          permission !== Permission.TenantOwnersManage ||
          (!editable && held.includes(permission)),
      ),
    }))
    .filter((group) => group.permissions.length > 0);
  return (
    <div className="flex flex-col gap-4">
      {groups.map((group) => (
        <fieldset
          key={group.id}
          className="min-w-0 rounded-lg border border-border bg-card"
        >
          {/* A floated legend sits inside the border, as the board's group title does. */}
          <legend className="float-start w-full border-b border-border px-4 py-2.5 text-body font-semibold">
            {t(`roles.groups.${group.id}`)}
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
      ))}
    </div>
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
  const id = useId();
  const reasonId = `${id}-reason`;
  const lock = editable ? lockOf(permission, held) : null;
  const lacks = editable && !lock && !editable.viewer.includes(permission);
  const reason = lock ? t(`roles.${lock}`) : lacks ? t("roles.lacks") : null;
  const key = `permissions.${permission}` as "permissions.tenant.audit.read";
  return (
    <div className="flex min-h-11 items-start gap-3 border-t border-border px-4 py-2.5 first:border-t-0">
      <input
        id={id}
        type="checkbox"
        className="mt-0.5 size-4.5 shrink-0 accent-primary"
        checked={held.includes(permission) || lock !== null}
        disabled={!editable || lock !== null || lacks}
        aria-describedby={reason ? reasonId : undefined}
        onChange={(event) =>
          editable?.onChange(permission, event.target.checked)
        }
      />
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <label
          htmlFor={id}
          className={cn(
            "flex items-center gap-1.5",
            lacks && "text-muted-foreground",
          )}
        >
          {t.has(key) ? t(key) : <bdi dir="ltr">{permission}</bdi>}
          {lock && (
            <Icon icon={LockIcon} size={14} className="text-muted-foreground" />
          )}
        </label>
        {reason && (
          <p
            id={reasonId}
            className={cn(
              "text-small",
              lacks ? "text-destructive-text" : "text-muted-foreground",
            )}
          >
            {reason}
          </p>
        )}
      </div>
    </div>
  );
}
