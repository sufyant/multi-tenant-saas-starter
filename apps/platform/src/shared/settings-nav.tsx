"use client";

import { SubNav } from "@repo/ui/components/sub-nav";
import Link from "next/link";
import { useTranslations } from "next-intl";

/** The addresses of the Settings pages. */
export const settingsPages = {
  preferences: "/settings/preferences",
  systemAdmins: "/settings/system-admins",
} as const;

/** The Settings SubNav of the platform (the Preferences and SystemAdmins boards): You › Preferences, Platform › System admins. */
export function SettingsNav({
  current,
}: {
  current: "preferences" | "system-admins";
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
          ],
        },
      ]}
      linkComponent={Link}
    />
  );
}
