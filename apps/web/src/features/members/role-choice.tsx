"use client";

import {
  getListRolesQueryOptions,
  type RoleResponse,
  schemas,
} from "@repo/api-client";
import { cn } from "@repo/ui/lib/utils";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { builtInRoleOf, inListOrder, isOwner } from "@/shared/roles";
import { useRoleName } from "@/shared/use-role-name";

/** The tenant's roles (ListRoles) on one page: the built-in roles and the few custom roles a tenant may have. */
export const listRolesOptions = (tenantId: string, enabled = true) =>
  getListRolesQueryOptions(
    tenantId,
    { pageSize: schemas.listRolesQueryPageSizeMax },
    { query: { enabled } },
  );

/** The roles a viewer may give, in list order: Owner only from an Owner (K1). */
export const rolesFor = <T extends RoleResponse>(
  roles: readonly T[],
  viewerIsOwner: boolean,
) => inListOrder(roles).filter((role) => viewerIsOwner || !isOwner(role));

/** The roles of the ChangeRole board: one radio for each, with its line of description for a built-in role. */
export function RoleChoice({
  roles,
  value,
  onChange,
}: {
  roles: RoleResponse[];
  /** The id of the chosen role. */
  value: string;
  onChange: (roleId: string) => void;
}) {
  const t = useTranslations();
  const roleName = useRoleName();
  const id = useId();
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="sr-only">{t("members.role")}</legend>
      {roles.map((role) => {
        const builtIn = builtInRoleOf(role);
        return (
          <label
            key={role.id}
            className={cn(
              "flex cursor-pointer items-start gap-2.5 rounded-md border border-border px-3 py-2.5",
              role.id === value && "border-primary ring-1 ring-primary",
            )}
          >
            <input
              type="radio"
              name="role"
              value={role.id}
              checked={role.id === value}
              onChange={() => onChange(role.id)}
              aria-labelledby={`${id}-${role.id}-name`}
              aria-describedby={builtIn ? `${id}-${role.id}` : undefined}
              className="mt-0.5 size-4 shrink-0 accent-primary"
            />
            <span className="flex flex-col">
              <span id={`${id}-${role.id}-name`} className="font-medium">
                {roleName(role)}
              </span>
              {builtIn && (
                <span
                  id={`${id}-${role.id}`}
                  className="text-small text-muted-foreground"
                >
                  {t(`members.roleDescriptions.${builtIn}`)}
                </span>
              )}
            </span>
          </label>
        );
      })}
    </fieldset>
  );
}
