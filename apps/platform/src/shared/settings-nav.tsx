"use client";

import { SubNav } from "@repo/ui/components/sub-nav";
import Link from "next/link";
import { useTranslations } from "next-intl";

/** The Settings SubNav of the platform (the Preferences and SystemAdmins boards): You › Preferences, Platform › System admins. */
export function SettingsNav({
  base,
  current,
}: {
  /** The address of Settings: `/settings`. */
  base: string;
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
              href: `${base}/preferences`,
            },
          ],
        },
        {
          label: t("platform"),
          items: [
            {
              id: "system-admins",
              label: t("systemAdmins"),
              href: `${base}/system-admins`,
            },
          ],
        },
      ]}
      linkComponent={Link}
    />
  );
}
