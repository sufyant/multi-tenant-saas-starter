import {
  type AuditActorResponse,
  AuditActorResponseKind,
  type AuditDetailsResponse,
  type AuditEntitlementValueResponse,
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

// The words of the log's sentences, for the screen (`activity-sentence.tsx`) and for the CSV file (`activity-text.ts`) alike.

/** The words for a person the log cannot name. */
export type Translate = (
  key: "activity.thePlatform" | "activity.deletedUser" | "activity.unnamedUser",
) => string;

/** Who acted: the platform by its own name, a deleted user as "Deleted user", else the user's real name (K3). */
export function actorName(actor: AuditActorResponse, t: Translate): string {
  if (actor.kind === AuditActorResponseKind.Platform)
    return t("activity.thePlatform");
  if (actor.deleted) return t("activity.deletedUser");
  return actor.displayName ?? t("activity.unnamedUser");
}

/** The person the action was done to. An invitation has no name, and a sentence about it names no one. */
export function targetName(target: AuditTargetResponse, t: Translate): string {
  if (target.deleted) return t("activity.deletedUser");
  return target.name ?? t("activity.unnamedUser");
}

/** The values of a sentence: a text, or a tag that writes a node (the screen's elements, or the file's text). */
export type SentenceValues<N> = Record<string, string | ((chunks: N) => N)>;

/** What a sentence of the plan names: a plan, a key and a value, each in the viewer's language. */
export type PlanWords<N> = {
  plan: (name: string) => () => N;
  key: (key: string) => () => N;
  value: (value: AuditEntitlementValueResponse) => string;
};

/**
 * The values of the details that a sentence shows: the roles, the names, the day of a purge and the plan's changes. The details'
 * own `operation` narrows the union. The reason of `tenant.suspended` never shows: a member never sees it (the Suspended board).
 * Nor does the reason of an override: the tenant's entry has none, and the sentence never writes one (the API's flow of plans).
 */
export function detailValues<N>(
  details: AuditDetailsResponse | null,
  role: (role: AuditRoleResponse) => () => N,
  permissions: (names: string[]) => () => N,
  date: (at: string) => string,
  words: PlanWords<N>,
): SentenceValues<N> {
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
export const keyLabels: Record<
  string,
  "rolesCustom" | "rolesCustomMax" | "membersMax"
> = {
  [FeatureKey.RolesCustom]: "rolesCustom",
  [LimitKey.RolesCustomMax]: "rolesCustomMax",
  [LimitKey.MembersMax]: "membersMax",
};

/** Which sides of a role's permissions changed: the `select` of the sentence of `role.updated`. */
export function changeOf(details: RoleUpdatedDetailsResponse): string {
  const added = details.permissionsAdded.length > 0;
  const removed = details.permissionsRemoved.length > 0;
  if (added && removed) return "both";
  if (added) return "added";
  return removed ? "removed" : "none";
}

/** A list shows this many permissions by name, and counts the rest (the Messages board). */
export const namedPermissions = 3;

/** A plan's value in the reader's words: unlimited, on or off, or the number. */
export function planValue(
  value: AuditEntitlementValueResponse,
  t: (
    key: "plans.values.unlimited" | "plans.values.on" | "plans.values.off",
  ) => string,
): string {
  if (value.unlimited) return t("plans.values.unlimited");
  if (value.enabled !== null)
    return t(value.enabled ? "plans.values.on" : "plans.values.off");
  return String(value.maximum);
}
