"use client";

import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { Input as InputBase } from "@repo/ui/components/ui/input";
import { Switch } from "@repo/ui/components/ui/switch";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { type Draft, useEntitlementText, valueFrom } from "./entitlements";

/**
 * The control of one key's value (the Plans frames and the OrgPlan board, F52): a `Switch` for a feature, with On or Off in the
 * override dialog; a whole number with an Unlimited `Checkbox` for a limit. Unlimited empties and locks the number. In the plan
 * editor (`showUnset`) the switch stands alone (6.04), a feature with no value says "Not set: closed" beside it and a limit with
 * no value shows its placeholder, "Not set" (6.10); an override always has a value, so its dialog leaves that out. A limit whose
 * feature is off (37d) is `disabled` with its value kept, and `note` under it says why (F52); an error takes the note's place.
 */
export function ValueField({
  isFeature,
  label,
  draft,
  onChange,
  error,
  showUnset = false,
  unit,
  disabled = false,
  note,
}: {
  isFeature: boolean;
  /** The accessible name of the switch and of the number. */
  label: string;
  draft: Draft;
  onChange: (draft: Draft) => void;
  error?: string;
  showUnset?: boolean;
  /** The unit after the number: the Activity log's days (6.04). */
  unit?: string;
  /** The value cannot change: the number and Unlimited are disabled, and the value stays shown. */
  disabled?: boolean;
  /** Why the value cannot change, under it in the muted colour. */
  note?: string;
}) {
  const t = useTranslations();
  const text = useEntitlementText();
  const unlimitedId = useId();
  const errorId = useId();
  const noteId = useId();
  const described = error ? errorId : note ? noteId : undefined;
  const unset = showUnset && isFeature && draft.enabled === null;
  return (
    <div
      className={
        showUnset
          ? "flex flex-col items-start gap-1.5"
          : "flex flex-col items-end gap-1.5"
      }
    >
      <div
        className={
          showUnset
            ? "flex min-h-control-md flex-wrap items-center gap-3"
            : "flex flex-wrap items-center justify-end gap-3"
        }
      >
        {isFeature ? (
          <>
            <Switch
              aria-label={label}
              checked={draft.enabled === true}
              onCheckedChange={(enabled) => onChange({ ...draft, enabled })}
            />
            {!showUnset && draft.enabled !== null && (
              <span className="min-w-8 text-body text-muted-foreground">
                {text.label(true, valueFrom(true, draft))}
              </span>
            )}
          </>
        ) : (
          <>
            <InputBase
              type="number"
              inputMode="numeric"
              min={0}
              step={1}
              aria-label={label}
              aria-invalid={error ? true : undefined}
              aria-describedby={described}
              placeholder={draft.unlimited ? "" : t("plans.numberPlaceholder")}
              disabled={disabled || draft.unlimited}
              value={draft.text}
              onChange={(event) =>
                onChange({ ...draft, text: event.target.value })
              }
              className={
                showUnset
                  ? "h-control-md w-30 rounded-md bg-card px-2.5 text-body tabular-nums pointer-coarse:h-control-lg"
                  : "h-control-md w-28 rounded-md bg-card px-3 text-body tabular-nums pointer-coarse:h-control-lg"
              }
            />
            {unit && <span className="text-body">{unit}</span>}
            <span className="flex items-center gap-2">
              <Checkbox
                id={unlimitedId}
                disabled={disabled}
                aria-describedby={described}
                checked={draft.unlimited}
                onCheckedChange={(unlimited) =>
                  onChange({ ...draft, unlimited, text: "" })
                }
              />
              <label
                htmlFor={unlimitedId}
                className={disabled ? "text-body opacity-45" : "text-body"}
              >
                {t("entitlements.unlimited")}
              </label>
            </span>
          </>
        )}
        {unset && <span className="text-body">{t("entitlements.notSet")}</span>}
      </div>
      {error && (
        <p id={errorId} className="text-small text-destructive-text">
          {error}
        </p>
      )}
      {!error && note && (
        // The note starts where the field starts, in the dialog too (4.51).
        <p id={noteId} className="self-start text-small text-muted-foreground">
          {note}
        </p>
      )}
    </div>
  );
}
