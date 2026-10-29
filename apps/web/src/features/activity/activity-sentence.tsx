"use client";

import {
  type AuditActorResponse,
  AuditActorResponseKind,
  type AuditDetailsResponse,
  type AuditEntitlementValueResponse,
  type AuditEntryResponse,
  type AuditRoleResponse,
  type AuditTargetResponse,
  FeatureKey,
  LimitKey,
  MemberInvitedDetailsResponseOperation,
  MemberRemovedDetailsResponseOperation,
  MemberRoleChangedDetailsResponseOperation,
  type RoleUpdatedDetailsResponse,
  RoleUpdatedDetailsResponseOperation,
  TenantCreatedDetailsResponseOperation,
  TenantEntitlementOverrideRemovedDetailsResponseOperation,
  TenantEntitlementOverrideSetDetailsResponseOperation,
  TenantPlanChangedDetailsResponseOperation,
  TenantPurgeWarnedDetailsResponseOperation,
  TenantRenamedDetailsResponseOperation,
} from "@repo/api-client";
import { formatDate } from "@repo/ui/lib/format-date-time";
import { useTranslations } from "next-intl";
import { Fragment, type ReactNode } from "react";
import { useRoleName } from "@/shared/use-role-name";

/** The words for a person the log cannot name. */
type Translate = (
  key: "activity.thePlatform" | "activity.deletedUser" | "activity.unnamedUser",
) => string;

/** Who acted: the platform by its own name, a deleted user as "Deleted user", else the user's real name (K3). */
function actorName(actor: AuditActorResponse, t: Translate): string {
  if (actor.kind === AuditActorResponseKind.Platform)
    return t("activity.thePlatform");
  if (actor.deleted) return t("activity.deletedUser");
  return actor.displayName ?? t("activity.unnamedUser");
}

/** The person the action was done to. An invitation has no name, and a sentence about it names no one. */
function targetName(target: AuditTargetResponse, t: Translate): string {
  if (target.deleted) return t("activity.deletedUser");
  return target.name ?? t("activity.unnamedUser");
}

type Rich = Record<string, string | ((chunks: ReactNode) => ReactNode)>;

/** What a sentence of the plan names: a plan, a key and a value, each in the viewer's language. */
type PlanWords = {
  plan: (name: string) => () => ReactNode;
  key: (key: string) => () => ReactNode;
  value: (value: AuditEntitlementValueResponse) => string;
};

/**
 * The values of the details that a sentence shows: the roles, the names, the day of a purge and the plan's changes. The details'
 * own `operation` narrows the union. The reason of `tenant.suspended` never shows: a member never sees it (the Suspended board).
 * Nor does the reason of an override: the tenant's entry has none, and the sentence never writes one (the API's flow of plans).
 */
function detailValues(
  details: AuditDetailsResponse | null,
  role: (role: AuditRoleResponse) => () => ReactNode,
  permissions: (names: string[]) => () => ReactNode,
  date: (at: string) => string,
  words: PlanWords,
): Rich {
  switch (details?.operation) {
    case TenantCreatedDetailsResponseOperation.TenantCreated:
      return { plan: "named", planName: words.plan(details.plan.name) };
    case TenantPlanChangedDetailsResponseOperation.TenantPlanChanged:
      return {
        oldPlan: words.plan(details.oldPlan.name),
        newPlan: words.plan(details.newPlan.name),
      };
    case TenantEntitlementOverrideSetDetailsResponseOperation.TenantEntitlementOverrideSet:
      return {
        key: words.key(details.key),
        value: words.value(details.newValue),
        expires: details.newExpiresAt ? "yes" : "no",
        date: details.newExpiresAt ? date(details.newExpiresAt) : "",
      };
    case TenantEntitlementOverrideRemovedDetailsResponseOperation.TenantEntitlementOverrideRemoved:
      return { key: words.key(details.key) };
    case MemberInvitedDetailsResponseOperation.MemberInvited:
    case MemberRemovedDetailsResponseOperation.MemberRemoved:
      return { role: role(details.role) };
    case MemberRoleChangedDetailsResponseOperation.MemberRoleChanged:
      return { oldRole: role(details.oldRole), newRole: role(details.newRole) };
    case TenantRenamedDetailsResponseOperation.TenantRenamed:
      return { oldName: details.oldName, newName: details.newName };
    case TenantPurgeWarnedDetailsResponseOperation.TenantPurgeWarned:
      return { date: date(details.purgesAt) };
    case RoleUpdatedDetailsResponseOperation.RoleUpdated:
      return {
        change: changeOf(details),
        added: permissions(details.permissionsAdded),
        removed: permissions(details.permissionsRemoved),
      };
    default:
      // An entry without details, such as a `tenant.created` written before plans, names no plan.
      return { plan: "none" };
  }
}

/** The message of a key the web knows; any other key shows as it is, never as a blank. */
const keyLabels: Record<
  string,
  "rolesCustom" | "rolesCustomMax" | "membersMax"
> = {
  [FeatureKey.RolesCustom]: "rolesCustom",
  [LimitKey.RolesCustomMax]: "rolesCustomMax",
  [LimitKey.MembersMax]: "membersMax",
};

/** Which sides of a role's permissions changed: the `select` of the sentence of `role.updated`. */
function changeOf(details: RoleUpdatedDetailsResponse): string {
  const added = details.permissionsAdded.length > 0;
  const removed = details.permissionsRemoved.length > 0;
  if (added && removed) return "both";
  if (added) return "added";
  return removed ? "removed" : "none";
}

/** A list shows this many permissions by name, and counts the rest (the Messages board). */
const namedPermissions = 3;

const person = (name: string) => () => (
  <bdi className="font-medium">{name}</bdi>
);

/**
 * One entry of the tenant log as a sentence (the Activity board): the actor, what they did and to whom, from the message of the
 * operation. It holds no email address: an invitation is "someone" or "an invitation" (K3). A date in it follows the viewer's
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
  const words: PlanWords = {
    plan: (name) => () => <bdi>{name}</bdi>,
    key: (key) => () => {
      const label = keyLabels[key];
      return label ? t(`plans.keys.${label}`) : <bdi dir="ltr">{key}</bdi>;
    },
    value: (value) => {
      if (value.unlimited) return t("plans.values.unlimited");
      if (value.enabled !== null)
        return t(value.enabled ? "plans.values.on" : "plans.values.off");
      return String(value.maximum);
    },
  };
  const people = {
    actor: person(actorName(entry.actor, t)),
    target: person(targetName(entry.target, t)),
    name: (chunks: ReactNode) => <bdi className="font-medium">{chunks}</bdi>,
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
          ...detailValues(details, role, permissions, date, words),
          ...people,
          actorKind: entry.actor.kind,
        })}
    </p>
  );
}
