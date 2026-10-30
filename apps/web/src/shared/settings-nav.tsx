"use client";

import { Permission } from "@repo/api-client";
import { SubNav, type SubNavGroup } from "@repo/ui/components/sub-nav";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useCan, useTenant } from "./tenant-context";

/** The settings pages of the SubNav. Each organisation page needs its read permission, and the pages of You none (F5). */
export type SettingsPage =
  | "general"
  | "members"
  | "roles"
  | "activity"
  | "preferences"
  | "notifications";

/**
 * The SubNav of Settings (the Settings boards): Organisation (General, Members, Roles, Activity) and You (Preferences, Notifications). An item shows
 * only with its read permission (F5), and a group without items does not show.
 */
export function SettingsNav({ current }: { current: SettingsPage }) {
  const t = useTranslations();
  const can = useCan();
  const { tenant } = useTenant();
  const base = `/${tenant.slug}/settings`;
  const organisation = [
    {
      id: "general",
      label: t("settings.general"),
      permission: Permission.TenantProfileRead,
    },
    {
      id: "members",
      label: t("settings.members"),
      permission: Permission.TenantMembersRead,
    },
    {
      id: "roles",
      label: t("settings.roles"),
      permission: Permission.TenantRolesRead,
    },
    {
      id: "activity",
      label: t("settings.activity"),
      permission: Permission.TenantAuditRead,
    },
  ] as const;
  const groups: SubNavGroup[] = [
    {
      label: t("settings.organisation"),
      items: organisation
        .filter((page) => can(page.permission))
        .map(({ id, label }) => ({ id, label, href: `${base}/${id}` })),
    },
    {
      label: t("shell.you"),
      items: [
        {
          id: "preferences",
          label: t("shell.preferences"),
          href: `${base}/preferences`,
        },
        {
          id: "notifications",
          label: t("shell.notifications"),
          href: `${base}/notifications`,
        },
      ],
    },
  ];
  return (
    <SubNav
      label={t("shell.settings")}
      current={current}
      groups={groups.filter((group) => group.items.length > 0)}
      linkComponent={Link}
    />
  );
}
