"use client";

import { MemberRole } from "@repo/api-client";
import { cn } from "@repo/ui/lib/utils";
import { useTranslations } from "next-intl";
import { useId } from "react";

/** The roles a viewer may give: Owner only from an Owner (K1). */
export const rolesFor = (viewerIsOwner: boolean) =>
  Object.values(MemberRole).filter(
    (role) => viewerIsOwner || role !== MemberRole.Owner,
  );

/** The roles of the ChangeRole board: one radio for each, with its line of description. */
export function RoleChoice({
  roles,
  value,
  onChange,
}: {
  roles: MemberRole[];
  value: MemberRole;
  onChange: (role: MemberRole) => void;
}) {
  const t = useTranslations();
  const id = useId();
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="sr-only">{t("members.role")}</legend>
      {roles.map((role) => (
        <label
          key={role}
          className={cn(
            "flex cursor-pointer items-start gap-2.5 rounded-md border border-border px-3 py-2.5",
            role === value && "border-primary ring-1 ring-primary",
          )}
        >
          <input
            type="radio"
            name="role"
            value={role}
            checked={role === value}
            onChange={() => onChange(role)}
            aria-labelledby={`${id}-${role}-name`}
            aria-describedby={`${id}-${role}`}
            className="mt-0.5 size-4 shrink-0 accent-primary"
          />
          <span className="flex flex-col">
            <span id={`${id}-${role}-name`} className="font-medium">
              {t(`shell.roles.${role}`)}
            </span>
            <span
              id={`${id}-${role}`}
              className="text-small text-muted-foreground"
            >
              {t(`members.roleDescriptions.${role}`)}
            </span>
          </span>
        </label>
      ))}
    </fieldset>
  );
}
