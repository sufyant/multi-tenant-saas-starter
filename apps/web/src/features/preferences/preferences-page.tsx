"use client";

import { getGetMeQueryOptions } from "@repo/api-client";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Time } from "@repo/ui/components/time";
import {
  ToggleGroup,
  ToggleGroupItem,
} from "@repo/ui/components/ui/toggle-group";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useTheme } from "next-themes";
import { useEffect, useId, useState } from "react";
import { ProblemScreen } from "@/shared/problem-screen";
import { SettingsNav } from "@/shared/settings-nav";

/**
 * Settings > Preferences (F31): the signed-in user's name, email, language and time zone from GetMe, read-only, and the theme,
 * which is saved on the device. The change of the language and time zone comes with the first form.
 */
export function PreferencesPage() {
  const t = useTranslations();
  const themeId = useId();
  const { theme = "system", setTheme } = useTheme();
  const me = useQuery(getGetMeQueryOptions());
  // The current time renders in the browser only, so the server and the browser never disagree on it.
  const [now, setNow] = useState<Date | null>(null);
  useEffect(() => setNow(new Date()), []);

  if (me.error) return <ProblemScreen error={me.error} />;
  const format = t("locale.format");

  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="preferences" />
      <section className="flex max-w-2xl flex-1 flex-col gap-4">
        <h1 className="text-page-title">{t("preferences.title")}</h1>
        <p className="text-body text-muted-foreground">
          {t("preferences.readOnly")}
        </p>
        {me.data ? (
          <dl className="grid grid-cols-1 gap-x-6 gap-y-4 text-body tablet:grid-cols-[12rem_1fr]">
            <dt className="text-muted-foreground">{t("preferences.name")}</dt>
            <dd>
              {[me.data.firstName, me.data.lastName].filter(Boolean).join(" ")}
            </dd>
            <dt className="text-muted-foreground">{t("preferences.email")}</dt>
            <dd>
              <bdi dir="ltr">{me.data.email}</bdi>
            </dd>
            <dt className="text-muted-foreground">
              {t("preferences.language")}
            </dt>
            <dd lang={me.data.language}>
              {t(`languages.${me.data.language}`)}
            </dd>
            <dt className="text-muted-foreground">
              {t("preferences.timeZone")}
            </dt>
            <dd className="flex flex-wrap items-center gap-2">
              <span>
                {me.data.timeZone ?? t("preferences.browserTimeZone")}
              </span>
              {now && (
                <span className="text-muted-foreground">
                  {t("preferences.now")}{" "}
                  <Time
                    at={now}
                    show="time"
                    locale={format}
                    timeZone={me.data.timeZone}
                  />
                </span>
              )}
            </dd>
            <dt id={themeId} className="text-muted-foreground">
              {t("preferences.theme")}
            </dt>
            <dd className="flex flex-col gap-1">
              <ToggleGroup
                aria-labelledby={themeId}
                value={[theme]}
                onValueChange={(value) =>
                  value[0] && setTheme(String(value[0]))
                }
                spacing={0}
                variant="outline"
              >
                {(["light", "dark", "system"] as const).map((choice) => (
                  <ToggleGroupItem key={choice} value={choice} className="px-3">
                    {t(`theme.${choice}`)}
                  </ToggleGroupItem>
                ))}
              </ToggleGroup>
              <span className="text-small text-muted-foreground">
                {t("preferences.themeHint")}
              </span>
            </dd>
          </dl>
        ) : (
          <Skeleton label={t("common.loading")} />
        )}
      </section>
    </div>
  );
}
