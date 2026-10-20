"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  changePreferences,
  getGetMeQueryKey,
  getGetMeQueryOptions,
  Language,
  type MeResponse,
  ProblemDetailsError,
  schemas,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Combobox } from "@repo/ui/components/combobox";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { browserTimeZone } from "@repo/ui/lib/format-date-time";
import { directionOf } from "@repo/ui/lib/locale";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { useMemo } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { setLocale } from "@/i18n/set-locale";
import { ProblemScreen } from "@/shared/problem-screen";
import { SettingsNav } from "@/shared/settings-nav";
import { useFailureText } from "@/shared/use-failure-text";
import { timeZoneOptions } from "./time-zones";

type Preferences = z.infer<typeof schemas.ChangePreferencesBody>;

/**
 * Settings > Preferences (the Preferences board): the person's language and time zone, which follow them to every organisation
 * and device. The theme is not here: it lives in the user menu, saved on the device.
 */
export function PreferencesPage() {
  const t = useTranslations();
  const me = useQuery(getGetMeQueryOptions());
  if (me.error) return <ProblemScreen error={me.error} />;
  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="preferences" />
      <section className="flex min-w-0 max-w-[640px] flex-1 flex-col gap-4">
        <div>
          <h1 className="text-page-title">{t("preferences.title")}</h1>
          <p className="mt-1 text-body text-muted-foreground">
            {t("preferences.text")}
          </p>
        </div>
        {me.data ? (
          <PreferencesForm me={me.data} />
        ) : (
          <Skeleton label={t("common.loading")} />
        )}
      </section>
    </div>
  );
}

/**
 * The form, with the generated schema of ChangePreferences (F11). A time zone the person never set is null: the device's zone
 * shows as the value, and null is saved again unless they choose a zone. After a save the screen reads GetMe again (F47), and a
 * new language sets the locale cookie and renders the page again (F13).
 */
function PreferencesForm({ me }: { me: MeResponse }) {
  const t = useTranslations();
  const locale = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();
  const failureText = useFailureText();
  const form = useForm<Preferences>({
    resolver: zodResolver(schemas.ChangePreferencesBody),
    defaultValues: { language: me.language, timeZone: me.timeZone },
  });
  const device = useMemo(browserTimeZone, []);
  const zones = useMemo(
    () =>
      timeZoneOptions({
        locale: t("locale.format"),
        now: new Date(),
        extra: [device, ...(me.timeZone ? [me.timeZone] : [])],
      }).map((option) =>
        option.id === device
          ? {
              ...option,
              label: t("preferences.thisDevice", { zone: option.label }),
            }
          : option,
      ),
    [t, device, me.timeZone],
  );
  const languages = Object.values(Language);

  const change = useMutation({
    mutationFn: (preferences: Preferences) => changePreferences(preferences),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
      showToast({ title: t("preferences.saved") });
      if (saved.language === locale) return;
      await setLocale(saved.language);
      router.refresh();
    },
    onError: (error) => {
      const action =
        error instanceof ProblemDetailsError
          ? actionFor(error.status, error.problem)
          : null;
      // A 401 goes to sign-in; a suspended user reads GetMe again, and the shell shows the suspended screen (F4, F47).
      if (action?.kind === "sign-in") return window.location.assign("/sign-in");
      if (action?.kind === "account-suspended")
        return void queryClient.invalidateQueries({
          queryKey: getGetMeQueryKey(),
        });
      const [code] =
        action?.kind === "field-errors" ? (action.fields.timeZone ?? []) : [];
      if (code)
        return form.setError("timeZone", { message: `validation.${code}` });
      showToast({
        title: t("preferences.notSaved"),
        description: failureText(error),
        tone: "destructive",
      });
    },
  });

  const submit = form.handleSubmit(
    (preferences) => change.isPending || change.mutate(preferences),
  );
  const timeZoneError = form.formState.errors.timeZone?.message;

  return (
    <form
      noValidate
      onSubmit={(event) => void submit(event)}
      className="flex flex-col gap-4 rounded-lg border border-border bg-card p-5"
    >
      <Combobox
        label={t("preferences.language")}
        value={form.watch("language")}
        options={languages.map((language) => ({
          id: language,
          label: t(`languages.${language}`),
          lang: language,
          dir: directionOf(language),
        }))}
        onValueChange={(id) => {
          const language = languages.find((choice) => choice === id);
          if (language) form.setValue("language", language);
        }}
        hint={t("preferences.languageHint")}
        searchPlaceholder={t("preferences.languageSearch")}
        emptyLabel={t("preferences.languageEmpty")}
      />
      <Combobox
        label={t("preferences.timeZone")}
        value={form.watch("timeZone") ?? device}
        options={zones}
        onValueChange={(id) => {
          form.clearErrors("timeZone");
          form.setValue("timeZone", id);
        }}
        hint={t("preferences.timeZoneHint")}
        error={
          // Zod's error map and the API's field codes both give a key of the validation messages (F12).
          timeZoneError && t(timeZoneError as "validation.time_zone_invalid")
        }
        searchPlaceholder={t("preferences.timeZoneSearch")}
        emptyLabel={t("preferences.timeZoneEmpty")}
      />
      <div className="flex justify-end">
        <Button type="submit" variant="primary" disabled={change.isPending}>
          {t("preferences.save")}
        </Button>
      </div>
    </form>
  );
}
