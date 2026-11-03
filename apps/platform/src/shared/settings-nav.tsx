"use client";

import { SubNav } from "@repo/ui/components/sub-nav";
import Link from "next/link";
import { useTranslations } from "next-intl";

/** The addresses of the Settings pages. */
export const settingsPages = {
  preferences: "/settings/preferences",
  systemAdmins: "/settings/system-admins",
  plans: "/settings/plans",
} as const;

/**
 * The Settings SubNav of the platform (the Preferences, SystemAdmins and Plans boards): You › Preferences, Platform › System admins
 * and Plans.
 */
export function SettingsNav({
  current,
}: {
  current: "preferences" | "system-admins" | "plans";
}) {
  const t = useTranslations("shell");
  return (
    <SubNav
      label={t("settings")}
      current={current}
      groups={[
        {
          label: t("you"),
          items: [
            {
              id: "preferences",
              label: t("preferences"),
              href: settingsPages.preferences,
            },
          ],
        },
        {
          label: t("platform"),
          items: [
            {
              id: "system-admins",
              label: t("systemAdmins"),
              href: settingsPages.systemAdmins,
            },
            {
              id: "plans",
              label: t("plans"),
              href: settingsPages.plans,
            },
          ],
        },
      ]}
      linkComponent={Link}
    />
  );
}
