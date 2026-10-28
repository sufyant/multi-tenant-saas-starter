"use client";

import { Status } from "@repo/ui/components/status";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { Input as InputBase } from "@repo/ui/components/ui/input";
import { Switch } from "@repo/ui/components/ui/switch";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { type Draft, useEntitlementText, valueFrom } from "./entitlements";

/**
 * The control of one key's value (the PlanEdit and OrgPlan boards, F52): a `Switch` with On or Off for a feature; a whole number
 * with an Unlimited `Checkbox` for a limit. Unlimited empties and locks the number. In the plan editor a key with no value says
 * "Not set: closed"; an override always has a value, so its dialog leaves that out.
 */
export function ValueField({
  isFeature,
  label,
  draft,
  onChange,
  error,
  showUnset = false,
}: {
  isFeature: boolean;
  /** The accessible name of the switch and of the number. */
  label: string;
  draft: Draft;
  onChange: (draft: Draft) => void;
  error?: string;
  showUnset?: boolean;
}) {
  const t = useTranslations();
  const text = useEntitlementText();
  const unlimitedId = useId();
  const errorId = useId();
  const unset = showUnset && valueFrom(isFeature, draft) === null && !error;
  return (
    <div className="flex flex-col items-end gap-1.5">
      <div className="flex flex-wrap items-center justify-end gap-3">
        {isFeature ? (
          <>
            <Switch
              aria-label={label}
              checked={draft.enabled === true}
              onCheckedChange={(enabled) => onChange({ ...draft, enabled })}
            />
            {draft.enabled !== null && (
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
              aria-describedby={error ? errorId : undefined}
              placeholder={draft.unlimited ? "" : t("plans.numberPlaceholder")}
              disabled={draft.unlimited}
              value={draft.text}
              onChange={(event) =>
                onChange({ ...draft, text: event.target.value })
              }
              className="h-control-md w-28 rounded-md bg-card px-3 text-body tabular-nums pointer-coarse:h-control-lg"
            />
            <span className="flex items-center gap-2">
              <Checkbox
                id={unlimitedId}
                checked={draft.unlimited}
                onCheckedChange={(unlimited) =>
                  onChange({ ...draft, unlimited, text: "" })
                }
              />
              <label htmlFor={unlimitedId} className="text-body">
                {t("entitlements.unlimited")}
              </label>
            </span>
          </>
        )}
        {unset && <Status tone="neutral">{t("entitlements.notSet")}</Status>}
      </div>
      {error && (
        <p id={errorId} className="text-small text-destructive-text">
          {error}
        </p>
      )}
    </div>
  );
}
