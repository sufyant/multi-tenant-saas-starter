import {
  type AuditEntryResponse,
  type AuditRoleResponse,
  RoleUpdatedDetailsResponseOperation,
} from "@repo/api-client";
import { formatDate } from "@repo/ui/lib/format-date-time";
import type { useTranslations } from "next-intl";
import { roleNameOf } from "@/shared/roles";
import {
  actorName,
  changeOf,
  detailValues,
  keyLabels,
  namedPermissions,
  type PlanWords,
  planValue,
  targetName,
} from "./activity-words";

/** A translator of every message of the web: the server's `getTranslations()`, or a test's `createTranslator`. */
export type Translator = ReturnType<typeof useTranslations<never>>;

/** The name of the person who acted, as the file's Who column writes it: the platform, "Deleted user" or the real name (K3). */
export const actorText = (entry: AuditEntryResponse, t: Translator) =>
  actorName(entry.actor, t);

/**
 * One entry of the tenant log as text, for the CSV file (12.08): the same words as the screen's `ActivitySentence`, from the same
 * messages and values, without the screen's elements and the Platform badge. A date in it follows `timeZone` (F18).
 */
export function sentenceText(
  entry: AuditEntryResponse,
  t: Translator,
  timeZone?: string | null,
): string {
  const locale = t("locale.format");
  const date = (at: string) => formatDate(at, { locale, timeZone });
  const role = (value: AuditRoleResponse) => () =>
    roleNameOf(value, (builtIn) => t(`shell.roleNames.${builtIn}`));
  // A permission by its label; a name the web does not know by itself, never a blank.
  const label = (name: string) => {
    const key = `permissions.${name}` as "permissions.tenant.audit.read";
    return t.has(key) ? t(key) : name;
  };
  const permissions = (names: string[]) => () => {
    const more = names.length - namedPermissions;
    const named = more > 0 ? names.slice(0, namedPermissions) : names;
    return new Intl.ListFormat(locale, { type: "conjunction" }).format([
      ...named.map(label),
      ...(more > 0 ? [t("activity.morePermissions", { count: more })] : []),
    ]);
  };
  const words: PlanWords<string> = {
    plan: (name) => () => name,
    key: (key) => () => {
      const keyLabel = keyLabels[key];
      return keyLabel ? t(`plans.keys.${keyLabel}`) : key;
    },
    value: (value) => planValue(value, t),
  };
  const people = {
    actor: () => actorName(entry.actor, t),
    target: () => targetName(entry.target, t),
    name: (chunks: string) => chunks,
  };
  const details = entry.details;
  // A rename of a role has a sentence of its own, before the sentence of its permissions when both changed.
  const renamed =
    details?.operation === RoleUpdatedDetailsResponseOperation.RoleUpdated &&
    details.oldName !== details.newName;
  const permissionsChanged =
    details?.operation !== RoleUpdatedDetailsResponseOperation.RoleUpdated ||
    changeOf(details) !== "none";
  const sentences: string[] = [];
  if (renamed)
    sentences.push(
      t.markup("activity.roleRenamed", {
        ...people,
        oldName: details.oldName,
        newName: details.newName,
      }),
    );
  if (!renamed || permissionsChanged)
    sentences.push(
      t.markup(`activity.operations.${entry.operation}`, {
        ...detailValues<string>(details, role, permissions, date, words),
        ...people,
        actorKind: entry.actor.kind,
      }),
    );
  return sentences.join(" ");
}
