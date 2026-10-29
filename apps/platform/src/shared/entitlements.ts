"use client";

import {
  type EntitlementValueRequest,
  type EntitlementValueResponse,
  FeatureKey,
  LimitKey,
} from "@repo/api-client";
import { useTranslations } from "next-intl";

// The entitlement keys of the generated client, and how the platform shows a key and its value (the Plans boards). A key added in
// code reaches every screen at once: the plan editor, the Plan section and Activity (`flows/plans-and-entitlements.md`).

/** A key's value on screen: the API's `{ enabled, maximum, unlimited }`, or null for no value (the key is closed). */
export type Value = Pick<
  EntitlementValueResponse,
  "enabled" | "maximum" | "unlimited"
> | null;

export type EntitlementKey = { key: string; isFeature: boolean; area: string };

/** The area of a key, which groups the editor: the first segment of the key (`roles.custom.max` is in `roles`). */
export const areaOf = (key: string) => key.split(".")[0] ?? key;

/** Every key of the list: the features, then the limits, in the order of the generated client. */
export const entitlementKeys: EntitlementKey[] = [
  ...Object.values(FeatureKey).map((key) => ({ key, isFeature: true })),
  ...Object.values(LimitKey).map((key) => ({ key, isFeature: false })),
].map((item) => ({ ...item, area: areaOf(item.key) }));

/** The keys by area, each area at the place of its first key. */
export const entitlementAreas = (): {
  area: string;
  keys: EntitlementKey[];
}[] =>
  [...new Set(entitlementKeys.map((item) => item.area))].map((area) => ({
    area,
    keys: entitlementKeys.filter((item) => item.area === area),
  }));

export const isFeatureKey = (key: string) =>
  entitlementKeys.find((item) => item.key === key)?.isFeature ?? false;

/** The body of one value: exactly one of `enabled`, `maximum` and `unlimited`, as the API takes it (`plan.entitlement_invalid`). */
export function requestOf(value: NonNullable<Value>): EntitlementValueRequest {
  if (value.unlimited) return { unlimited: true };
  if (value.maximum !== null) return { maximum: value.maximum };
  return { enabled: value.enabled ?? false };
}

/** A limit's room: unlimited is infinite, a number is itself, no value is 0 (the key is closed). */
const roomOf = (value: Value) =>
  value?.unlimited ? Number.POSITIVE_INFINITY : (value?.maximum ?? 0);

/**
 * Whether a new value takes something away: a feature that stops being on, or a limit that gets smaller. No value is closed, so
 * clearing a key lowers it (E3).
 */
export function lowers(isFeature: boolean, before: Value, after: Value) {
  if (isFeature) return before?.enabled === true && after?.enabled !== true;
  return roomOf(after) < roomOf(before);
}

/** A key as a message key: the dots of `roles.custom.max` would nest it. */
const messageKeyOf = (key: string) => key.replaceAll(".", "_");

/**
 * The texts of the keys and their values. A key without a text of its own (a key the platform does not know yet) shows the key
 * itself, so it still shows.
 */
export function useEntitlementText() {
  const t = useTranslations("entitlements");
  const keyText = (key: string, part: "name" | "description") => {
    const path =
      `keys.${messageKeyOf(key)}.${part}` as "keys.roles_custom.name";
    return t.has(path) ? t(path) : part === "name" ? key : "";
  };
  return {
    name: (key: string) => keyText(key, "name"),
    description: (key: string) => keyText(key, "description"),
    area: (area: string) => {
      const path = `areas.${area}` as "areas.roles";
      return t.has(path) ? t(path) : area;
    },
    /** A value in a table or a field: `On`, `Unlimited`, `20`, `Not set: closed`. */
    label: (isFeature: boolean, value: Value) => {
      if (!value) return t("notSet");
      if (isFeature) return value.enabled ? t("on") : t("off");
      return value.unlimited ? t("unlimited") : String(value.maximum ?? 0);
    },
    /** A value inside a sentence of Activity: `on`, `unlimited`, `20`, `not set`. */
    inSentence: (isFeature: boolean, value: Value) => {
      if (!value) return t("sentence.notSet");
      if (isFeature)
        return value.enabled ? t("sentence.on") : t("sentence.off");
      return value.unlimited
        ? t("sentence.unlimited")
        : String(value.maximum ?? 0);
    },
    /** What turning a feature off leaves (the OrgPlan board's change of plan), for a key that has its own text. */
    turnsOff: (key: string, count: number) => {
      const path =
        `keys.${messageKeyOf(key)}.turnsOff` as "keys.roles_custom.turnsOff";
      const values = { key: keyText(key, "name"), count };
      return t.has(path) ? t(path, values) : t("turnsOff", values);
    },
  };
}

/**
 * A value as a form holds it: a feature's state, or null before anyone sets it; a limit's text as typed, so an empty field and a
 * wrong number stay on screen; and the Unlimited box.
 */
export type Draft = {
  enabled: boolean | null;
  text: string;
  unlimited: boolean;
};

export const draftOf = (value: Value): Draft => ({
  enabled: value?.enabled ?? null,
  text:
    value?.maximum === null || value?.maximum === undefined
      ? ""
      : String(value.maximum),
  unlimited: value?.unlimited ?? false,
});

/** The value a draft gives: no feature state and an empty limit are no value. Check the text first (`isWholeNumber`). */
export function valueFrom(isFeature: boolean, draft: Draft): Value {
  if (isFeature)
    return draft.enabled === null
      ? null
      : { enabled: draft.enabled, maximum: null, unlimited: false };
  if (draft.unlimited) return { enabled: null, maximum: null, unlimited: true };
  if (draft.text === "") return null;
  return { enabled: null, maximum: Number(draft.text), unlimited: false };
}

/** The largest limit the API takes: an `int32`. */
const largest = 2_147_483_647;

/** A limit's text the API takes: empty (no value), or a whole number from 0 (`plan.entitlement_invalid`). */
export const isWholeNumber = (text: string) =>
  /^\d*$/.test(text) && (text === "" || Number(text) <= largest);
