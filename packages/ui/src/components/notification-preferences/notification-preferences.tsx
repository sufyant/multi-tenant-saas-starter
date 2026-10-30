"use client";

import type { NotificationCategory } from "@repo/api-client";
import { useTranslations } from "next-intl";
import { type ReactNode, useId, useState } from "react";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import { showToast } from "../toast";
import { Switch } from "../ui/switch";

/** The email preferences as GetEmailPreferences answers them, and as ChangeEmailPreferences takes them. */
export type EmailPreferences = {
  enabled: boolean;
  categories: { category: NotificationCategory; enabled: boolean }[];
};

export type NotificationPreferencesProps = {
  /** The answer of GetEmailPreferences: only the categories that can reach the person. */
  values: EmailPreferences;
  /** Says under the main switch that removal always emails: when "Your access" is listed. */
  removedNote: boolean;
  /** ChangeEmailPreferences. */
  save: (preferences: EmailPreferences) => Promise<unknown>;
  /** After the save: read GetEmailPreferences again (F47). */
  onSaved: () => Promise<void>;
  /** A failed save: sign-in, the account's screen or a toast (F4). */
  onFailed: (error: unknown) => void;
};

/**
 * Settings › You › Notifications (the NotificationPrefs boards of both apps): the email main switch, and under it one switch per
 * category the API lists, with its name and description from `notificationCategories` (F14). In-app notifications cannot be turned
 * off, so the page holds email only. A switch is a value of the form and applies on Save (F47). With the main switch off, the
 * categories show off and disabled, and each keeps its choice, so turning the main switch on again brings it back as saved.
 */
export function NotificationPreferences({
  values,
  removedNote,
  save,
  onSaved,
  onFailed,
}: NotificationPreferencesProps) {
  const t = useTranslations();
  const headingId = useId();
  const [main, setMain] = useState(values.enabled);
  const [chosen, setChosen] = useState(values.categories);
  const [saving, setSaving] = useState(false);

  const submit = async () => {
    setSaving(true);
    try {
      await save({ enabled: main, categories: chosen });
    } catch (error) {
      setSaving(false);
      return onFailed(error);
    }
    await onSaved();
    setSaving(false);
    showToast({ title: t("notificationPreferences.saved") });
  };

  return (
    <form
      noValidate
      aria-labelledby={headingId}
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
      className="flex flex-col rounded-lg border border-border bg-card px-5 pt-1 pb-5"
    >
      <h2 id={headingId} className="mt-4 text-section-title">
        {t("notificationPreferences.email")}
      </h2>
      <SwitchRow
        main
        label={t("notificationPreferences.main")}
        description={t("notificationPreferences.mainText")}
        checked={main}
        onChange={setMain}
      />
      {removedNote && (
        <p className="-mt-1.5 mb-2.5 text-small text-muted-foreground">
          {t("notificationPreferences.removedNote")}
        </p>
      )}
      <div className="ms-0.5 border-s-2 border-border ps-4">
        {chosen.map(({ category, enabled }) => (
          <SwitchRow
            key={category}
            label={t(`notificationCategories.${category}.name`)}
            description={t(`notificationCategories.${category}.description`)}
            checked={main && enabled}
            disabled={!main}
            onChange={(on) =>
              setChosen((before) =>
                before.map((choice) =>
                  choice.category === category
                    ? { ...choice, enabled: on }
                    : choice,
                ),
              )
            }
          />
        ))}
      </div>
      {!main && (
        <p className="mt-1 text-small text-muted-foreground">
          {t("notificationPreferences.offHint")}
        </p>
      )}
      <div className="mt-3 flex justify-end border-t border-border pt-4">
        <Button type="submit" variant="primary" disabled={saving}>
          {t("notificationPreferences.save")}
        </Button>
      </div>
    </form>
  );
}

/** A setting that a switch turns on or off (the design system's `.st-swrow`): the label names the switch, the description describes it. */
function SwitchRow({
  main = false,
  label,
  description,
  checked,
  disabled = false,
  onChange,
}: {
  main?: boolean;
  label: string;
  description: ReactNode;
  checked: boolean;
  disabled?: boolean;
  onChange: (on: boolean) => void;
}) {
  const id = useId();
  return (
    <div className="flex items-start justify-between gap-4 py-3 [&+&]:border-t [&+&]:border-border">
      <div className="min-w-0">
        <label
          htmlFor={id}
          className={cn(
            "block text-body",
            main ? "font-semibold" : "font-medium",
            disabled
              ? "cursor-not-allowed text-muted-foreground"
              : "cursor-pointer",
          )}
        >
          {label}
        </label>
        <p
          id={`${id}-description`}
          className="mt-0.5 text-small text-muted-foreground"
        >
          {description}
        </p>
      </div>
      <Switch
        id={id}
        aria-describedby={`${id}-description`}
        checked={checked}
        disabled={disabled}
        onCheckedChange={onChange}
      />
    </div>
  );
}
