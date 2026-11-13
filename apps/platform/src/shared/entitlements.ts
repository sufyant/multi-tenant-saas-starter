"use client";

import {
  AuditRetention,
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

/** The areas in the order of the Plans frames (6.04): Members, then Roles. An area the frames do not draw follows them. */
const drawnAreas = ["members", "roles"];

/** The keys by area: the frames' areas first, in their order; any other area at the place of its first key. */
export const entitlementAreas = (): {
  area: string;
  keys: EntitlementKey[];
}[] =>
  [...new Set([...drawnAreas, ...entitlementKeys.map((item) => item.area)])]
    .map((area) => ({
      area,
      keys: entitlementKeys.filter((item) => item.area === area),
    }))
    .filter(({ keys }) => keys.length > 0);

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

/** The limit of the Activity log's period, in days: a whole number from the floor, or unlimited (the API's K14). */
export const retentionKey: string = LimitKey.AuditRetentionDays;

/** A period under the floor, which the API refuses (`plan.entitlement_invalid`): the screen refuses it before it sends. */
export const belowFloor = (key: string, draft: Draft) =>
  key === retentionKey &&
  !draft.unlimited &&
  /^\d+$/.test(draft.text) &&
  Number(draft.text) < AuditRetention.Floor;

/** A key as a message key: the dots of `roles.custom.max` would nest it. */
const messageKeyOf = (key: string) => key.replaceAll(".", "_");

/**
 * The texts of the keys and their values. A key without a text of its own (a key the platform does not know yet) shows the key
 * itself, so it still shows.
 */
export function useEntitlementText() {
  const t = useTranslations("entitlements");
  /** The Activity log's days: `365 days`, in the plural of each language. */
  const days = (count: number) =>
    t("keys.audit_retention_days.days", { count });
  // The floor and the hold of the Activity log's period, which its texts say (`@repo/api-client`'s generated constants).
  const periods = {
    floor: AuditRetention.Floor,
    floorDays: days(AuditRetention.Floor),
    grace: days(AuditRetention.Grace),
    graceCount: AuditRetention.Grace,
  };
  const keyText = (key: string, part: "name" | "description" | "row") => {
    const path =
      `keys.${messageKeyOf(key)}.${part}` as "keys.audit_retention_days.description";
    if (t.has(path)) return t(path, periods);
    if (part === "row") return keyText(key, "name");
    return part === "name" ? key : "";
  };
  /** A limit's number, which the Activity log says in days. */
  const numberOf = (key: string | undefined, maximum: number) =>
    key === retentionKey ? days(maximum) : String(maximum);
  return {
    name: (key: string) => keyText(key, "name"),
    /** The name of a key's row in the plan editor, under its area: the Activity log's one row is "Keeps entries" (6.04). */
    row: (key: string) => keyText(key, "row"),
    description: (key: string) => keyText(key, "description"),
    days,
    periods,
    /** The unit after a key's number in the plan editor: the Activity log's days; none for another key. */
    unit: (key: string, text: string) =>
      key === retentionKey
        ? t("keys.audit_retention_days.unit", {
            count: /^\d+$/.test(text) ? Number(text) : 0,
          })
        : undefined,
    /** Why a key's number is refused: the floor of the Activity log, else a whole number from 0. */
    floor: () => t("keys.audit_retention_days.floor", periods),
    area: (area: string) => {
      const path = `areas.${area}` as "areas.roles";
      return t.has(path) ? t(path) : area;
    },
    /**
     * A value in a table or a field: `On`, `Unlimited`, `20`, `Not set: closed`. With its key, the Activity log says its days, and no
     * value keeps every entry (E3).
     */
    label: (isFeature: boolean, value: Value, key?: string) => {
      if (!value)
        return key === retentionKey
          ? t("keys.audit_retention_days.notSet")
          : t("notSet");
      if (isFeature) return value.enabled ? t("on") : t("off");
      return value.unlimited
        ? t("unlimited")
        : numberOf(key, value.maximum ?? 0);
    },
    /** A value inside a sentence of Activity: `on`, `unlimited`, `20`, `365 days`, `not set`. */
    inSentence: (isFeature: boolean, value: Value, key?: string) => {
      if (!value) return t("sentence.notSet");
      if (isFeature)
        return value.enabled ? t("sentence.on") : t("sentence.off");
      return value.unlimited
        ? t("sentence.unlimited")
        : numberOf(key, value.maximum ?? 0);
    },
    /** What turning a feature off on a plan leaves (4.15), for a key that has its own text. */
    turnsOff: (key: string, count: number, plan: string) => {
      const path =
        `keys.${messageKeyOf(key)}.turnsOff` as "keys.roles_custom.turnsOff";
      const values = { key: keyText(key, "name"), count, plan };
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
