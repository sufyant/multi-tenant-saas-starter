"use client";

import {
  AuditActorResponseKind,
  type AuditEntryResponse,
  type AuditRoleResponse,
  RoleUpdatedDetailsResponseOperation,
} from "@repo/api-client";
import { formatDate } from "@repo/ui/lib/format-date-time";
import { useTranslations } from "next-intl";
import { Fragment, type ReactNode } from "react";
import { useRoleName } from "@/shared/use-role-name";
import {
  actorName,
  changeOf,
  detailValues,
  inviteeValues,
  keyLabels,
  namedPermissions,
  type PlanWords,
  planValue,
  targetName,
} from "./activity-words";

const person = (name: string) => () => (
  <bdi className="font-medium">{name}</bdi>
);

/**
 * One entry of the tenant log as a sentence (the Activity board): the actor, what they did and to whom, from the message of the
 * operation. An invitation is named by its address, which the API reads now, or "someone" once it is erased (K3, A8). A date in it follows the viewer's
 * zone (`timeZone`, from GetMe) and the language's format, as the other dates of Activity do (F18).
 */
export function ActivitySentence({
  entry,
  timeZone,
}: {
  entry: AuditEntryResponse;
  timeZone?: string | null;
}) {
  const t = useTranslations();
  const date = (at: string) =>
    formatDate(at, { locale: t("locale.format"), timeZone });
  const roleName = useRoleName();
  // A built-in role by its translated name; a custom role by the name the entry keeps, isolated from the sentence's direction. The
  // entry's `builtIn` decides, never the name (F51).
  const role = (value: AuditRoleResponse) => () =>
    value.builtIn ? roleName(value) : <bdi>{roleName(value)}</bdi>;
  // A permission by its label; a name the web does not know by itself, never a blank.
  const label = (name: string) => {
    const key = `permissions.${name}` as "permissions.tenant.audit.read";
    return t.has(key) ? t(key) : <bdi dir="ltr">{name}</bdi>;
  };
  // The list follows the language's format (`locale.format`), as the dates do: "A, B and C" in en-IE.
  const list = (items: ReactNode[]) =>
    new Intl.ListFormat(t("locale.format"), { type: "conjunction" })
      .formatToParts(items.map((_, index) => String(index)))
      .map((part, index) =>
        part.type === "element" ? (
          // biome-ignore lint/suspicious/noArrayIndexKey: the parts of one list never move.
          <Fragment key={index}>{items[Number(part.value)]}</Fragment>
        ) : (
          part.value
        ),
      );
  const permissions = (names: string[]) => () => {
    const more = names.length - namedPermissions;
    const named = more > 0 ? names.slice(0, namedPermissions) : names;
    return list([
      ...named.map(label),
      ...(more > 0 ? [t("activity.morePermissions", { count: more })] : []),
    ]);
  };
  const words: PlanWords<ReactNode> = {
    plan: (name) => () => <bdi>{name}</bdi>,
    key: (key) => () => {
      const label = keyLabels[key];
      return label ? t(`plans.keys.${label}`) : <bdi dir="ltr">{key}</bdi>;
    },
    value: (value, key) => planValue(value, key, t),
  };
  const people = {
    actor: person(actorName(entry.actor, t)),
    target: person(targetName(entry.target, t)),
    name: (chunks: ReactNode) => <bdi className="font-medium">{chunks}</bdi>,
    invitee: (chunks: ReactNode) => (
      <bdi dir="ltr" className="font-medium">
        {chunks}
      </bdi>
    ),
  };
  const details = entry.details;
  // A rename of a role has a sentence of its own, before the sentence of its permissions when both changed.
  const renamed =
    details?.operation === RoleUpdatedDetailsResponseOperation.RoleUpdated &&
    details.oldName !== details.newName;
  const permissionsChanged =
    details?.operation !== RoleUpdatedDetailsResponseOperation.RoleUpdated ||
    changeOf(details) !== "none";
  return (
    <p className="min-w-0">
      {entry.actor.kind === AuditActorResponseKind.Platform && (
        <>
          <span className="rounded-sm bg-platform px-1.5 py-0.5 text-status text-platform-foreground">
            {t("activity.platform")}
          </span>{" "}
        </>
      )}
      {renamed &&
        t.rich("activity.roleRenamed", {
          ...people,
          oldName: details.oldName,
          newName: details.newName,
        })}
      {renamed && permissionsChanged && " "}
      {(!renamed || permissionsChanged) &&
        t.rich(`activity.operations.${entry.operation}`, {
          ...detailValues<ReactNode>(details, role, permissions, date, words),
          ...inviteeValues(entry),
          ...people,
          actorKind: entry.actor.kind,
        })}
    </p>
  );
}
