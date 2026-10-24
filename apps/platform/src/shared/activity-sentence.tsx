"use client";

import {
  AuditActorResponseKind,
  type AuditDetailsResponse,
  type AuditEntryResponse,
  type AuditTargetResponse,
  AuditTargetResponseKind,
  TenantOwnerMadeDetailsResponseOperation,
  TenantPurgeWarnedDetailsResponseOperation,
  TenantRenamedDetailsResponseOperation,
  TenantSuspendedDetailsResponseOperation,
} from "@repo/api-client";
import Link from "next/link";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { useDates } from "./use-dates";

/** The page of a target: an organisation's or a user's. A deleted user, an invitation and a role have none. */
function hrefOf(target: AuditTargetResponse): string | null {
  if (target.deleted) return null;
  if (target.kind === AuditTargetResponseKind.Tenant)
    return `/organisations/${target.id}`;
  if (target.kind === AuditTargetResponseKind.User)
    return `/users/${target.id}`;
  return null;
}

const linkClass = "font-medium text-primary underline-offset-4 hover:underline";

const linked = (href: string | null, content: ReactNode) =>
  href ? (
    <Link href={href} className={linkClass}>
      <bdi>{content}</bdi>
    </Link>
  ) : (
    <bdi className="font-medium">{content}</bdi>
  );

type Rich = Record<string, string | ((chunks: ReactNode) => ReactNode)>;

/**
 * The values of the details that a sentence shows. The details' own `operation` narrows the union. The platform's log keeps the
 * reason of a suspension (the API's catalog), and a system admin may read it.
 */
function detailValues(
  details: AuditDetailsResponse | null,
  date: (at: string) => string,
): Rich {
  switch (details?.operation) {
    case TenantRenamedDetailsResponseOperation.TenantRenamed:
      return { oldName: details.oldName, newName: details.newName };
    case TenantSuspendedDetailsResponseOperation.TenantSuspended:
      return { reason: details.reason };
    case TenantPurgeWarnedDetailsResponseOperation.TenantPurgeWarned:
      return { date: date(details.purgesAt) };
    case TenantOwnerMadeDetailsResponseOperation.TenantOwnerMade:
      // The details name the member by id only; the API adds no name (section 10 of the frontend architecture).
      return {
        member: (chunks) => linked(`/users/${details.userId}`, chunks),
      };
    default:
      return {};
  }
}

/**
 * One entry of the platform's log as a sentence (the Activity board, P6): who acted, what they did and to what, from the message
 * of the operation. It names people by their name, never an email (K3). An entry the platform wrote itself has no person. A target
 * organisation or user links to its page; a deleted user does not. An operation the platform does not know says so, with its name.
 */
export function ActivitySentence({ entry }: { entry: AuditEntryResponse }) {
  const t = useTranslations();
  const { date } = useDates();
  const { actor, target } = entry;
  const actorName =
    actor.kind === AuditActorResponseKind.Platform
      ? t("activity.thePlatform")
      : actor.deleted
        ? t("activity.deletedUser")
        : (actor.displayName ?? t("activity.unnamedUser"));
  const targetName = target.deleted
    ? t("activity.deletedUser")
    : (target.name ?? t("activity.unnamedUser"));
  const key = `activity.operations.${entry.operation}` as "activity.title";
  const values: Rich = {
    ...detailValues(entry.details, date),
    actor: () => <bdi className="font-medium">{actorName}</bdi>,
    target: () => linked(hrefOf(target), targetName),
    link: (chunks) => linked(hrefOf(target), chunks),
    name: (chunks) => <bdi>{chunks}</bdi>,
    code: (chunks) => (
      <bdi dir="ltr" className="font-mono text-mono">
        {chunks}
      </bdi>
    ),
    actorKind: actor.kind,
    self: actor.userId === target.id ? "yes" : "no",
    operation: entry.operation,
  };
  return (
    <p className="min-w-0">
      {t.has(key)
        ? t.rich(key, values)
        : t.rich("activity.operations.unknown", values)}
    </p>
  );
}
