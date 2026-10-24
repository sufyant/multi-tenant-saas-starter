"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import type { Language } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { browserTimeZone } from "../../lib/format-date-time";
import { directionOf } from "../../lib/locale";
import { Button } from "../button";
import { Combobox } from "../combobox";
import { Input } from "../input";
import { showToast } from "../toast";
import { timeZoneOptions } from "./time-zones";

export type Names = { firstName: string; lastName: string };
export type Preferences = { language: Language; timeZone: string | null };
export type PreferencesValues = Names & Preferences;

/** What one Save sends: only the parts that changed. */
export type PreferencesChanges = { names?: Names; preferences?: Preferences };

export type PreferencesFormProps = {
  /** The values of GetMe. A name the user has not got is "". */
  values: PreferencesValues;
  /** The generated schemas of RenameMe and ChangePreferences as one object, with the names trimmed and required (F11). */
  schema: z.ZodType<PreferencesValues, PreferencesValues>;
  /** The languages of the contract: `Object.values(Language)`. */
  languages: readonly Language[];
  /** Under the names: who sees them. Each app has its own. */
  nameNote: string;
  rename: (names: Names) => Promise<unknown>;
  changePreferences: (preferences: Preferences) => Promise<unknown>;
  /** The API's field codes of a refusal, keyed by the field's JSON name, else null (F4). */
  fieldCodesOf: (error: unknown) => Partial<Record<string, string[]>> | null;
  /** After at least one call succeeded: read GetMe again (F47) and follow a new language (F13). */
  onSaved: (saved: PreferencesChanges) => Promise<void>;
  /** A failure with no field code of its own: sign-in, the suspended screen or a toast (F4). */
  onFailed: (error: unknown) => void;
};

/** The parts of the values that differ from the saved ones. */
export function changesOf(
  saved: PreferencesValues,
  values: PreferencesValues,
): PreferencesChanges {
  const { firstName, lastName, language, timeZone } = values;
  const renamed = firstName !== saved.firstName || lastName !== saved.lastName;
  const changed = language !== saved.language || timeZone !== saved.timeZone;
  return {
    ...(renamed && { names: { firstName, lastName } }),
    ...(changed && { preferences: { language, timeZone } }),
  };
}

const nameFields = ["firstName", "lastName"] as const;
const preferenceFields = ["timeZone"] as const;
const required = {
  firstName: "preferencesForm.firstNameRequired",
  lastName: "preferencesForm.lastNameRequired",
} as const;

/**
 * Settings > Preferences (the Preferences boards of both apps): the person's name (RenameMe), language and time zone
 * (ChangePreferences), with one Save that sends only what changed. The two calls run side by side and the form waits for both
 * (F47). A call that succeeds stays saved when the other fails; the failed call's field codes show under its fields. A time
 * zone the person never set is null: the device's zone shows as the value, and null stays unless they choose a zone.
 */
export function PreferencesForm({
  values,
  schema,
  languages,
  nameNote,
  rename,
  changePreferences,
  fieldCodesOf,
  onSaved,
  onFailed,
}: PreferencesFormProps) {
  const t = useTranslations();
  const [saved, setSaved] = useState(values);
  const form = useForm<PreferencesValues>({
    resolver: zodResolver(schema),
    defaultValues: values,
  });
  const device = useMemo(browserTimeZone, []);
  const zones = useMemo(
    () =>
      timeZoneOptions({
        locale: t("locale.format"),
        now: new Date(),
        extra: [device, ...(values.timeZone ? [values.timeZone] : [])],
      }).map((option) =>
        option.id === device
          ? {
              ...option,
              label: t("preferencesForm.thisDevice", { zone: option.label }),
            }
          : option,
      ),
    [t, device, values.timeZone],
  );

  /** Sends one part; on a refusal its field codes go under its fields, and any other failure goes back to the caller. */
  async function send<T>(
    part: T | undefined,
    call: (part: T) => Promise<unknown>,
    fields: readonly (keyof PreferencesValues)[],
  ): Promise<{ sent: boolean; failure?: unknown }> {
    if (!part) return { sent: false };
    try {
      await call(part);
      return { sent: true };
    } catch (error) {
      const codes = fieldCodesOf(error);
      const refused = fields.filter((field) => codes?.[field]?.[0]);
      for (const field of refused)
        form.setError(field, { message: `validation.${codes?.[field]?.[0]}` });
      return { sent: false, failure: refused.length ? undefined : error };
    }
  }

  const submit = form.handleSubmit(async (next) => {
    const changes = changesOf(saved, next);
    if (!changes.names && !changes.preferences) return;
    const [named, changed] = await Promise.all([
      send(changes.names, rename, nameFields),
      send(changes.preferences, changePreferences, preferenceFields),
    ]);
    const done: PreferencesChanges = {
      ...(named.sent && { names: changes.names }),
      ...(changed.sent && { preferences: changes.preferences }),
    };
    if (done.names || done.preferences) {
      setSaved((before) => ({ ...before, ...done.names, ...done.preferences }));
      await onSaved(done);
    }
    const failure = named.failure ?? changed.failure;
    if (failure) return onFailed(failure);
    const failed =
      (changes.names && !named.sent) || (changes.preferences && !changed.sent);
    if (!failed) showToast({ title: t("preferencesForm.saved") });
  });

  const { errors, isSubmitting } = form.formState;
  // Zod's error map and the API's field codes both give a key of the validation messages (F12). A name has texts of its own.
  const nameError = (field: (typeof nameFields)[number]) => {
    const key = errors[field]?.message;
    if (key === "validation.required") return t(required[field]);
    if (key === "validation.too_long") return t("preferencesForm.nameTooLong");
    return key && t(key as "validation.invalid");
  };
  const timeZoneError = errors.timeZone?.message;

  return (
    <form
      noValidate
      onSubmit={(event) => void submit(event)}
      className="flex flex-col gap-5 rounded-lg border border-border bg-card p-5"
    >
      <section
        aria-labelledby="preferences-name"
        className="flex flex-col gap-3"
      >
        <h2 id="preferences-name" className="text-section-title">
          {t("preferencesForm.yourName")}
        </h2>
        <div className="grid grid-cols-1 gap-3 tablet:grid-cols-2">
          <Input
            label={t("preferencesForm.firstName")}
            autoComplete="given-name"
            error={nameError("firstName")}
            {...form.register("firstName")}
          />
          <Input
            label={t("preferencesForm.lastName")}
            autoComplete="family-name"
            error={nameError("lastName")}
            {...form.register("lastName")}
          />
        </div>
        <p className="text-small text-muted-foreground">{nameNote}</p>
      </section>
      <section
        aria-labelledby="preferences-language"
        className="flex flex-col gap-4 border-t border-border pt-5"
      >
        <h2 id="preferences-language" className="text-section-title">
          {t("preferencesForm.languageAndTimeZone")}
        </h2>
        <Combobox
          label={t("preferencesForm.language")}
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
          hint={t("preferencesForm.languageHint")}
          searchPlaceholder={t("preferencesForm.languageSearch")}
          emptyLabel={t("preferencesForm.languageEmpty")}
        />
        <Combobox
          label={t("preferencesForm.timeZone")}
          value={form.watch("timeZone") ?? device}
          options={zones}
          onValueChange={(id) => {
            form.clearErrors("timeZone");
            form.setValue("timeZone", id);
          }}
          hint={t("preferencesForm.timeZoneHint")}
          error={
            timeZoneError && t(timeZoneError as "validation.time_zone_invalid")
          }
          searchPlaceholder={t("preferencesForm.timeZoneSearch")}
          emptyLabel={t("preferencesForm.timeZoneEmpty")}
        />
      </section>
      <div className="flex justify-end border-t border-border pt-4">
        <Button type="submit" variant="primary" disabled={isSubmitting}>
          {t("preferencesForm.save")}
        </Button>
      </div>
    </form>
  );
}
