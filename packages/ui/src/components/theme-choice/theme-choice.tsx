"use client";

import { useTranslations } from "next-intl";
import { useTheme } from "next-themes";
import { useId } from "react";
import { ToggleGroup, ToggleGroupItem } from "../ui/toggle-group";

/** The choices, in the order of the Profile page: System first, the default (F24). */
export const themes = ["system", "light", "dark"] as const;

/**
 * The theme of the Profile page in both apps (F24): System, Light or Dark, saved on this device by next-themes. System follows
 * the device and is the default.
 */
export function ThemeChoice() {
  const t = useTranslations("theme");
  const id = useId();
  const { theme = "system", setTheme } = useTheme();
  return (
    <div className="flex flex-col gap-1.5">
      <span id={id} className="text-body font-medium">
        {t("label")}
      </span>
      <ToggleGroup
        aria-labelledby={id}
        value={[theme]}
        onValueChange={(value) => value[0] && setTheme(String(value[0]))}
        spacing={0}
        variant="outline"
        className="w-fit"
      >
        {themes.map((choice) => (
          <ToggleGroupItem key={choice} value={choice} className="px-3">
            {t(choice)}
          </ToggleGroupItem>
        ))}
      </ToggleGroup>
      <span className="text-small text-muted-foreground">{t("hint")}</span>
    </div>
  );
}
