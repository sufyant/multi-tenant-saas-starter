"use client";

import {
  type AuditActorResponse,
  AuditActorResponseKind,
  type AuditDetailsResponse,
  type AuditEntryResponse,
  type AuditTargetResponse,
  MemberInvitedDetailsResponseOperation,
  MemberRemovedDetailsResponseOperation,
  MemberRoleChangedDetailsResponseOperation,
  type RoleResponse,
  TenantRenamedDetailsResponseOperation,
} from "@repo/api-client";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";

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

/**
 * The values of the details that a sentence shows: the roles and the names. The details' own `operation` narrows the union. The
 * reason of `tenant.suspended` never shows: a member never sees it (the Suspended board).
 */
function detailValues(
  details: AuditDetailsResponse | null,
  role: (role: RoleResponse) => string,
): Record<string, string> {
  switch (details?.operation) {
    case MemberInvitedDetailsResponseOperation.MemberInvited:
    case MemberRemovedDetailsResponseOperation.MemberRemoved:
      return { role: role(details.role) };
    case MemberRoleChangedDetailsResponseOperation.MemberRoleChanged:
      return { oldRole: role(details.oldRole), newRole: role(details.newRole) };
    case TenantRenamedDetailsResponseOperation.TenantRenamed:
      return { oldName: details.oldName, newName: details.newName };
    default:
      return {};
  }
}

const person = (name: string) => () => (
  <bdi className="font-medium">{name}</bdi>
);

/**
 * One entry of the tenant log as a sentence (the Activity board): the actor, what they did and to whom, from the message of the
 * operation. It holds no email address: an invitation is "someone" or "an invitation" (K3).
 */
export function ActivitySentence({ entry }: { entry: AuditEntryResponse }) {
  const t = useTranslations();
  const role = (value: RoleResponse) =>
    value.builtIn ? t(`shell.roles.${value.builtIn}`) : value.name;
  return (
    <p className="min-w-0">
      {entry.actor.kind === AuditActorResponseKind.Platform && (
        <>
          <span className="rounded-sm bg-platform px-1.5 py-0.5 text-status text-platform-foreground">
            {t("activity.platform")}
          </span>{" "}
        </>
      )}
      {t.rich(`activity.operations.${entry.operation}`, {
        ...detailValues(entry.details, role),
        actor: person(actorName(entry.actor, t)),
        target: person(targetName(entry.target, t)),
        name: (chunks: ReactNode) => (
          <bdi className="font-medium">{chunks}</bdi>
        ),
      })}
    </p>
  );
}
