"use client";

import { Permission } from "@repo/api-client";
import { SubNav, type SubNavGroup } from "@repo/ui/components/sub-nav";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useCan, useTenant } from "./tenant-context";

/** The settings pages of the SubNav. Each needs its read permission, and Preferences none (F5). */
export type SettingsPage = "members" | "preferences";

/**
 * The SubNav of Settings (the Members boards): Organisation (Members) and You (Preferences). An item shows only with its read
 * permission (F5), and a group without items does not show. General and Activity join when their pages come (section 10).
 */
export function SettingsNav({ current }: { current: SettingsPage }) {
  const t = useTranslations();
  const can = useCan();
  const { tenant } = useTenant();
  const base = `/${tenant.slug}/settings`;
  const groups: SubNavGroup[] = [
    {
      label: t("settings.organisation"),
      items: can(Permission.TenantMembersRead)
        ? [
            {
              id: "members",
              label: t("settings.members"),
              href: `${base}/members`,
            },
          ]
        : [],
    },
    {
      label: t("shell.you"),
      items: [
        {
          id: "preferences",
          label: t("shell.preferences"),
          href: `${base}/preferences`,
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
