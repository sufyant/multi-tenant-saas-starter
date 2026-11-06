"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import type { Language } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { type ReactNode, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { browserTimeZone } from "../../lib/format-date-time";
import { directionOf } from "../../lib/locale";
import { Button } from "../button";
import { Combobox } from "../combobox";
import { Input } from "../input";
import { showToast } from "../toast";
import { Skeleton as SkeletonBlock } from "../ui/skeleton";
import { timeZoneOptions } from "./time-zones";

export type Names = { name: string };
export type Preferences = { language: Language; timeZone: string | null };
export type PreferencesValues = Names & Preferences;

/** What one Save sends: only the parts that changed. */
export type PreferencesChanges = { names?: Names; preferences?: Preferences };

export type PreferencesFormProps = {
  /** The values of GetMe. A name the user has not got is "". */
  values: PreferencesValues;
  /** The generated schemas of RenameMe and ChangePreferences as one object, with the name trimmed and required (F11). */
  schema: z.ZodType<PreferencesValues, PreferencesValues>;
  /** The most characters of the name: the generated schema's maximum (`schemas.renameMeBodyNameMax`). */
  nameMax: number;
  /** The languages of the contract: `Object.values(Language)`. */
  languages: readonly Language[];
  /** Under the name: who sees it. Each app has its own. */
  nameNote: string;
  /** Under "Profile saved" after a new name: where it shows (5.04). Without it the toast has its title only. */
  savedNote?: string;
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
  const { name, language, timeZone } = values;
  const renamed = name !== saved.name;
  const changed = language !== saved.language || timeZone !== saved.timeZone;
  return {
    ...(renamed && { names: { name } }),
    ...(changed && { preferences: { language, timeZone } }),
  };
}

const nameFields = ["name"] as const;
const preferenceFields = ["timeZone"] as const;

/**
 * The card of Settings › Profile (the Profile screens of both apps): the person's name (RenameMe), language and time zone
 * (ChangePreferences), each 360 px wide from bp-tablet, and Save changes in the card's foot, which stays off until a value
 * changes and sends only what changed. The two calls run side by side and the form waits for both
 * (F47). A call that succeeds stays saved when the other fails; the failed call's field codes show under its fields. A time
 * zone the person never set is null: the device's zone shows as the value, and null stays unless they choose a zone.
 */
export function PreferencesForm({
  values,
  schema,
  nameMax,
  languages,
  nameNote,
  savedNote,
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
    if (failed) return;
    // Everything is saved: the values are the form's new start, so Save changes is off again.
    form.reset(next);
    showToast({
      title: t("preferencesForm.saved"),
      ...(done.names && savedNote && { description: savedNote }),
    });
  });

  const { errors, isSubmitting, isDirty } = form.formState;
  // Zod's error map and the API's field codes both give a key of the validation messages (F12). A name has texts of its own.
  const nameError = () => {
    const key = errors.name?.message;
    if (key === "validation.required") return t("preferencesForm.nameRequired");
    if (key === "validation.too_long")
      return t("preferencesForm.nameTooLong", { max: nameMax });
    return key && t(key as "validation.invalid");
  };
  const timeZoneError = errors.timeZone?.message;

  return (
    <form
      noValidate
      onSubmit={(event) => void submit(event)}
      className="flex flex-col rounded-lg border border-border bg-card shadow-xs"
    >
      <div className="flex flex-col gap-5 p-5 [&>*]:tablet:max-w-[360px]">
        <Input
          label={t("preferencesForm.name")}
          autoComplete="name"
          hint={nameNote}
          error={nameError()}
          {...form.register("name")}
        />
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
            if (language)
              form.setValue("language", language, { shouldDirty: true });
          }}
          hint={<LanguageNote languages={languages} />}
          searchPlaceholder={t("preferencesForm.languageSearch")}
          emptyLabel={t("preferencesForm.languageEmpty")}
        />
        <Combobox
          label={t("preferencesForm.timeZone")}
          value={form.watch("timeZone") ?? device}
          options={zones}
          onValueChange={(id) => {
            form.clearErrors("timeZone");
            form.setValue("timeZone", id, { shouldDirty: true });
          }}
          hint={t("preferencesForm.timeZoneHint")}
          error={
            timeZoneError && t(timeZoneError as "validation.time_zone_invalid")
          }
          searchPlaceholder={t("preferencesForm.timeZoneSearch")}
          emptyLabel={t("preferencesForm.timeZoneEmpty")}
        />
      </div>
      <Foot>
        <Button
          type="submit"
          variant="primary"
          disabled={isSubmitting || !isDirty}
          className="max-tablet:w-full"
        >
          {t("preferencesForm.save")}
        </Button>
      </Foot>
    </form>
  );
}

/** The card's foot: a line above it, Save changes at the end; on a phone across the card (5.09). */
function Foot({ children }: { children: ReactNode }) {
  return (
    <div className="flex justify-end border-t border-border px-5 py-3">
      {children}
    </div>
  );
}

/**
 * The language's note names Arabic in its own script and direction, so it reads right to left in every language (F14). Arabic is
 * the contract's language that reads right to left; this package names no contract value (F3).
 */
function LanguageNote({ languages }: { languages: readonly Language[] }) {
  const t = useTranslations();
  const rightToLeft = languages.find(
    (language) => directionOf(language) === "rtl",
  );
  return t.rich("preferencesForm.languageHint", {
    arabic: (text) => (
      <span lang={rightToLeft} dir="rtl">
        {text}
      </span>
    ),
  });
}

/** The card while GetMe loads (5.05): the labels and the notes in their places, a bar for each value, and Save changes off. */
export function PreferencesFormSkeleton({
  nameNote,
  languages,
}: {
  nameNote: string;
  /** The languages of the contract: `Object.values(Language)`. */
  languages: readonly Language[];
}) {
  const t = useTranslations();
  const field = (label: string, note: ReactNode) => (
    <div className="flex flex-col gap-1.5 tablet:max-w-[360px]">
      <span className="text-small font-medium">{label}</span>
      <SkeletonBlock
        aria-hidden="true"
        className="h-control-md w-full [animation-duration:1.4s] motion-reduce:animate-none"
      />
      <span className="text-small text-muted-foreground">{note}</span>
    </div>
  );
  return (
    <div
      role="status"
      aria-busy="true"
      aria-label={t("common.loading")}
      className="flex flex-col rounded-lg border border-border bg-card shadow-xs"
    >
      <div className="flex flex-col gap-5 p-5">
        {field(t("preferencesForm.name"), nameNote)}
        {field(
          t("preferencesForm.language"),
          <LanguageNote languages={languages} />,
        )}
        {field(
          t("preferencesForm.timeZone"),
          t("preferencesForm.timeZoneHint"),
        )}
      </div>
      <Foot>
        <Button variant="primary" disabled className="max-tablet:w-full">
          {t("preferencesForm.save")}
        </Button>
      </Foot>
    </div>
  );
}
