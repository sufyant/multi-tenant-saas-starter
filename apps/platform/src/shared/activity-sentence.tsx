"use client";

import {
  AuditActorResponseKind,
  type AuditDetailsResponse,
  type AuditEntitlementValueResponse,
  type AuditEntryResponse,
  type AuditPlanResponse,
  type AuditTargetResponse,
  AuditTargetResponseKind,
  PlanUpdatedDetailsResponseOperation,
  TenantCreatedDetailsResponseOperation,
  TenantEntitlementOverrideRemovedDetailsResponseOperation,
  TenantEntitlementOverrideSetDetailsResponseOperation,
  TenantOwnerInvitedDetailsResponseOperation,
  TenantOwnerMadeDetailsResponseOperation,
  TenantPlanChangedDetailsResponseOperation,
  TenantPurgeWarnedDetailsResponseOperation,
  TenantRenamedDetailsResponseOperation,
  TenantSuspendedDetailsResponseOperation,
  TermsVersionCancelledDetailsResponseOperation,
  TermsVersionCreatedDetailsResponseOperation,
  TermsVersionUpdatedDetailsResponseOperation,
} from "@repo/api-client";
import { useTranslations } from "next-intl";
import { Fragment, type ReactNode } from "react";
import {
  entitlementKeys,
  isFeatureKey,
  useEntitlementText,
} from "./entitlements";
import { isolated } from "./isolated";
import { planPage } from "./plans";
import { termsVersionPage, useDay } from "./terms";
import { TextLink } from "./text-link";
import { useDates } from "./use-dates";

/** The page of a target: an organisation's, a user's or a plan's. A deleted user, an invitation and a role have none. */
function hrefOf(target: AuditTargetResponse): string | null {
  if (target.deleted) return null;
  if (target.kind === AuditTargetResponseKind.Tenant)
    return `/organisations/${target.id}`;
  if (target.kind === AuditTargetResponseKind.User)
    return `/users/${target.id}`;
  if (target.kind === AuditTargetResponseKind.Plan) return planPage(target.id);
  if (target.kind === AuditTargetResponseKind.TermsVersion)
    return termsVersionPage(target.id);
  return null;
}

/** A name in a sentence: its weight marks a name, and the underline of `TextLink` marks a link (WCAG 2.2, 1.4.1). */
const linked = (href: string | null, content: ReactNode) => {
  const name = <bdi className="font-medium">{content}</bdi>;
  return href ? <TextLink href={href}>{name}</TextLink> : name;
};

type Rich = Record<
  string,
  string | number | ((chunks: ReactNode) => ReactNode)
>;

/** A plan of the details, with its name as it was then, linked to its page. */
const planLink = (plan: AuditPlanResponse) => () =>
  linked(planPage(plan.id), plan.name);

const same = (
  a: AuditEntitlementValueResponse | undefined,
  b: AuditEntitlementValueResponse | undefined,
) =>
  a?.enabled === b?.enabled &&
  a?.maximum === b?.maximum &&
  a?.unlimited === b?.unlimited;

type Texts = {
  /** One change of a plan edit: `Custom roles limit from 20 to 10`, each value isolated (a name may be in another script). */
  change: (values: { key: string; old: string; new: string }) => ReactNode;
  separator: string;
  /** The plan's name as the key of a change, and no earlier value. */
  nameKey: string;
  none: string;
  text: ReturnType<typeof useEntitlementText>;
  date: (at: string) => string;
  /** A terms version's day, a calendar day of UTC. */
  day: (day: string) => string;
  /** The words of a terms version's edit, joined as the language joins a list: "the link, the summary and the date (before: …)". */
  termsChanges: (words: string[]) => string;
  termsWord: (
    word: "date" | "link" | "summary" | "requirement",
    old?: string,
  ) => string;
};

/**
 * The values of the details that a sentence shows. The details' own `operation` narrows the union. The platform's log keeps the
 * reason of a suspension (the API's catalog), and a system admin may read it.
 */
function detailValues(
  details: AuditDetailsResponse | null,
  {
    change: changeText,
    separator,
    nameKey,
    none,
    text,
    date,
    day,
    termsChanges,
    termsWord,
  }: Texts,
): Rich {
  const valueText = (
    key: string,
    value: AuditEntitlementValueResponse | null,
  ) => text.inSentence(isFeatureKey(key), value, key);
  switch (details?.operation) {
    case TenantCreatedDetailsResponseOperation.TenantCreated:
      return { onPlan: "yes", plan: planLink(details.plan) };
    case TenantPlanChangedDetailsResponseOperation.TenantPlanChanged:
      return {
        oldPlan: planLink(details.oldPlan),
        newPlan: planLink(details.newPlan),
      };
    case TenantEntitlementOverrideSetDetailsResponseOperation.TenantEntitlementOverrideSet:
      return {
        key: text.name(details.key),
        value: valueText(details.key, details.newValue),
        expiry: details.newExpiresAt ? "some" : "none",
        date: details.newExpiresAt ? date(details.newExpiresAt) : "",
        previous: details.oldValue
          ? valueText(details.key, details.oldValue)
          : none,
        // The tenant's log keeps no reason (the API's catalog), so its sentence has none. A reason that ends a sentence of its own
        // takes no second full stop.
        hasReason: !details.reason
          ? "no"
          : /[.!?…]$/.test(details.reason)
            ? "ended"
            : "yes",
        reason: details.reason ?? "",
      };
    case TenantEntitlementOverrideRemovedDetailsResponseOperation.TenantEntitlementOverrideRemoved:
      return {
        key: text.name(details.key),
        value: valueText(details.key, details.oldValue),
        expiry: details.oldExpiresAt ? "some" : "none",
        date: details.oldExpiresAt ? date(details.oldExpiresAt) : "",
      };
    case PlanUpdatedDetailsResponseOperation.PlanUpdated: {
      // Each value that changed, in the order of the editor, then the name (the platform's Activity boards).
      const known = entitlementKeys.map((item) => item.key);
      const keys = [
        ...known,
        ...[
          ...Object.keys(details.oldEntitlements),
          ...Object.keys(details.newEntitlements),
        ]
          .filter((key) => !known.includes(key))
          .sort(),
      ];
      const items = [...new Set(keys)]
        .filter(
          (key) =>
            !same(details.oldEntitlements[key], details.newEntitlements[key]),
        )
        .map((key) =>
          changeText({
            key: text.name(key),
            old: valueText(key, details.oldEntitlements[key] ?? null),
            new: valueText(key, details.newEntitlements[key] ?? null),
          }),
        );
      const renamed = details.oldName !== details.newName;
      if (renamed)
        items.push(
          changeText({
            key: nameKey,
            old: details.oldName,
            new: details.newName,
          }),
        );
      const previous = details.previousDefaultPlan;
      const change = previous
        ? items.length > 0
          ? "defaultAndChanges"
          : "default"
        : renamed && items.length === 1
          ? "renamed"
          : items.length === 1
            ? "one"
            : "other";
      return {
        change,
        oldName: details.oldName,
        changes: () =>
          items.map((item, index) => (
            // The items of one entry never reorder, so their place is a stable key.
            // biome-ignore lint/suspicious/noArrayIndexKey: see above
            <Fragment key={index}>
              {index > 0 && separator}
              {item}
            </Fragment>
          )),
        ...(previous ? { previous: planLink(previous) } : {}),
      };
    }
    case TermsVersionCreatedDetailsResponseOperation.TermsVersionCreated:
      // The two switches give the four sentences of 7.09.
      return {
        date: day(details.effectiveDate),
        kind: details.requiresAcceptance
          ? details.sendsEmail
            ? "both"
            : "mustAccept"
          : details.sendsEmail
            ? "email"
            : "none",
      };
    case TermsVersionUpdatedDetailsResponseOperation.TermsVersionUpdated: {
      // The version by its day after the edit, and each value that changed, in the order of the form (11.10). The email switch
      // turned on is its own sentence (7.09): it sent the email.
      const { old, new: next } = details;
      const words = [
        ...(old.effectiveDate !== next.effectiveDate
          ? [termsWord("date", day(old.effectiveDate))]
          : []),
        ...(old.url !== next.url ? [termsWord("link")] : []),
        ...(old.summary !== next.summary ? [termsWord("summary")] : []),
        ...(old.requiresAcceptance !== next.requiresAcceptance
          ? [termsWord("requirement")]
          : []),
      ];
      return {
        date: day(next.effectiveDate),
        change:
          words.length === 0 && !old.sendsEmail && next.sendsEmail
            ? "emailed"
            : "other",
        changes: termsChanges(words),
      };
    }
    case TermsVersionCancelledDetailsResponseOperation.TermsVersionCancelled:
      return {
        date: day(details.effectiveDate),
        count: details.acceptanceCount,
      };
    case TenantRenamedDetailsResponseOperation.TenantRenamed:
      return { oldName: details.oldName, newName: details.newName };
    case TenantSuspendedDetailsResponseOperation.TenantSuspended:
      return { reason: details.reason };
    case TenantPurgeWarnedDetailsResponseOperation.TenantPurgeWarned:
      return { date: date(details.purgesAt) };
    case TenantOwnerInvitedDetailsResponseOperation.TenantOwnerInvited:
      // The API reads the address now from the invitation (K3); once a user's deletion erased it (A8), the sentence says "someone".
      return details.email
        ? { addressed: "yes", address: details.email }
        : { addressed: "no", address: "" };
    case TenantOwnerMadeDetailsResponseOperation.TenantOwnerMade: {
      // The new Owner has the actor's shape (the API's step r13). A deleted user has no page, and the sentence of each language
      // names them, as it names a member whose name is null.
      if (details.deleted)
        return {
          owner: "deletedUser",
          member: (chunks) => linked(null, chunks),
        };
      return {
        owner: details.displayName === null ? "unnamed" : "named",
        member: (chunks) =>
          linked(`/users/${details.userId}`, details.displayName ?? chunks),
      };
    }
    default:
      return {};
  }
}

/**
 * One entry of the platform's log as a sentence (the platform's Activity boards): who acted, what they did and to what, from the message
 * of the operation. It names people by their name, never an email; an invitation by its address, which the API reads now (K3). An entry the platform wrote itself has no person. A target
 * organisation or user links to its page; a deleted user does not. An operation the platform does not know says so, with its name.
 */
export function ActivitySentence({ entry }: { entry: AuditEntryResponse }) {
  const t = useTranslations();
  const { date } = useDates();
  const day = useDay();
  const text = useEntitlementText();
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
  const key = `activity.operations.${entry.operation}` as "activity.restarted";
  const values: Rich = {
    onPlan: "none",
    addressed: "no",
    address: "",
    ...detailValues(entry.details, {
      change: (values) => t.rich("activity.change", { ...values, ...isolated }),
      separator: t("activity.changeSeparator"),
      nameKey: t("activity.nameKey"),
      none: t("entitlements.sentence.none"),
      text,
      date,
      day,
      termsChanges: (words) =>
        new Intl.ListFormat(t("locale.format"), {
          type: "conjunction",
        }).format(words),
      termsWord: (word, old) =>
        t(`activity.termsChanges.${word}`, { old: old ?? "" }),
    }),
    actor: () => <bdi className="font-medium">{actorName}</bdi>,
    target: () => linked(hrefOf(target), targetName),
    link: (chunks) => linked(hrefOf(target), chunks),
    name: (chunks) => <bdi>{chunks}</bdi>,
    invitee: (chunks) => (
      <bdi dir="ltr" className="font-medium">
        {chunks}
      </bdi>
    ),
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
