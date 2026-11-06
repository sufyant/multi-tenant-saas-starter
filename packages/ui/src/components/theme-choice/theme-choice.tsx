"use client";

import { useTranslations } from "next-intl";
import { useTheme } from "next-themes";
import { MonitorIcon, MoonIcon, SunIcon } from "../icon/icons";
import { SegmentedControl } from "../segmented-control";

/** The choices, in the order of the Profile page: System first, the default (F24). */
export const themes = ["system", "light", "dark"] as const;

type Theme = (typeof themes)[number];

const icons = { system: MonitorIcon, light: SunIcon, dark: MoonIcon };

/**
 * The theme of the Profile page in both apps (F24): System, Light or Dark, saved on this device by next-themes. System follows
 * the device and is the default. It is its own card under the form, because it applies at once and Save changes does not send it.
 */
export function ThemeChoice() {
  const t = useTranslations("theme");
  const { theme = "system", setTheme } = useTheme();
  return (
    <section className="flex flex-col gap-1.5 rounded-lg border border-border bg-card p-5">
      <span className="text-body font-medium">{t("label")}</span>
      <SegmentedControl
        label={t("label")}
        value={theme as Theme}
        options={themes.map((choice) => ({
          value: choice,
          label: t(choice),
          icon: icons[choice],
        }))}
        onValueChange={setTheme}
        size="adaptive"
        className="w-full tablet:w-fit"
      />
      <span className="text-small text-muted-foreground">{t("hint")}</span>
    </section>
  );
}
