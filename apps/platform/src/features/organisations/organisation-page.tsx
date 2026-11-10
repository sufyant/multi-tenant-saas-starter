"use client";

import {
  ErrorCode,
  getGetTenantPlanQueryOptions,
  getGetTenantQueryOptions,
  InvitationResponseState,
  problemOf,
  schemas,
  type TenantDetailResponse,
  TenantResponseStatus,
  type UserNameResponse,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { CopyButton } from "@repo/ui/components/copy-button";
import { EmptyValue } from "@repo/ui/components/empty-value";
import { ArrowLeftIcon, Icon } from "@repo/ui/components/icon";
import { Pill } from "@repo/ui/components/pill";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Skeleton } from "@repo/ui/components/skeleton";
import { buttonVariants } from "@repo/ui/components/ui/button";
import { useOrganisationAddress } from "@repo/ui/lib/organisation-address";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { parseAsStringLiteral, useQueryState } from "nuqs";
import type { ReactNode } from "react";
import { nameOf } from "@/shared/name-of";
import { NotFoundPage } from "@/shared/not-found-page";
import {
  PlatformActivity,
  usePlatformActivity,
} from "@/shared/platform-activity";
import { ProblemScreen } from "@/shared/problem-screen";
import { TextLink } from "@/shared/text-link";
import { useDates } from "@/shared/use-dates";
import { ViewChoice } from "@/shared/view-choice";
import { useMembersText } from "./members-text";
import {
  actionsOf,
  isGone,
  statuses,
  waitingInvitation,
  waitsForOwner,
} from "./organisation";
import { usePersonName } from "./person-name";
import { PlanSection } from "./plan-section";
import { useOrganisationActions } from "./use-organisation-actions";

/** The Users list, searched by the invited email: an Owner who joined as a member is made an Owner from their user page. */
const usersFoundBy = (email: string | undefined) =>
  email ? `/users?${new URLSearchParams({ q: email })}` : "/users";

/**
 * The tabs of an organisation (4.09, 4.14, 4.18). The tab lives in the URL, as every tab of the app (F9). Plan is "Plan and billing"
 * from step 40; Usage is step 40c. An organisation whose purge has begun has no plan to change (4.25, 4.26).
 */
const views = ["overview", "plan", "activity"] as const;
const viewParser = parseAsStringLiteral(views).withDefault("overview");

/**
 * One organisation (4.09 to 4.26, 4.44): Back to Organisations, the title with its status, its menu and its address, the banner of its
 * state, and the tabs. Overview holds its details from GetTenant and its plan's name and seats from GetTenantPlan; Plan holds the plan
 * and the overrides; Activity log holds the platform's entries about it, also for a deleted organisation. Every date and every "by"
 * comes from the answer: a "by" the API does not know is left out, never a fixed text (37b-backend).
 */
export function OrganisationPage({ tenantId }: { tenantId: string }) {
  const known =
    schemas.GetTenantParams.shape.tenantId.safeParse(tenantId).success;
  const tenant = useQuery(
    getGetTenantQueryOptions(tenantId, { query: { enabled: known } }),
  );
  if (!known || problemOf(tenant.error)?.code === ErrorCode.TenantNotFound)
    return <NotFoundPage of="organisation" />;
  if (tenant.error) return <ProblemScreen error={tenant.error} />;
  if (!tenant.data) return <Loading />;
  return <Organisation organisation={tenant.data} />;
}

function Loading() {
  const t = useTranslations("common");
  return <Skeleton rows={4} label={t("loading")} />;
}

function Organisation({
  organisation,
}: {
  organisation: TenantDetailResponse;
}) {
  const t = useTranslations();
  const { date } = useDates();
  const personName = usePersonName();
  const actions = useOrganisationActions();
  const address = useOrganisationAddress(organisation.slug);
  const { name, status } = organisation;
  const [view, setView] = useQueryState(
    "tab",
    viewParser.withOptions({ history: "push" }),
  );
  const gone = isGone(organisation);
  const shown = gone && view === "plan" ? "overview" : view;
  const invitation = waitingInvitation(organisation);
  const by = (
    key: "suspendedOn" | "closedOn",
    at: string,
    person: UserNameResponse | null,
  ) =>
    person
      ? t(`organisations.${key}By`, {
          date: date(at),
          name: personName(person),
        })
      : t(`organisations.${key}`, { date: date(at) });

  const banner = (() => {
    if (waitsForOwner(organisation)) {
      const expired = invitation?.state === InvitationResponseState.Expired;
      return (
        <Banner
          tone={expired ? "warning" : "info"}
          title={t("organisations.waitingTitle")}
        >
          {invitation &&
            t(
              expired
                ? "organisations.waitingExpired"
                : "organisations.waitingPending",
              { email: invitation.email, date: date(invitation.expiresAt) },
            )}
        </Banner>
      );
    }
    if (status === TenantResponseStatus.Suspended)
      return (
        <Banner
          tone="warning"
          title={
            organisation.suspendedAt
              ? by(
                  "suspendedOn",
                  organisation.suspendedAt,
                  organisation.suspendedBy,
                )
              : t("organisations.statusSuspended")
          }
          action={
            <Button onClick={() => actions.start("reactivate", organisation)}>
              {t("organisations.reactivate")}
            </Button>
          }
        >
          {organisation.suspensionReason
            ? t("organisations.suspendedText", {
                reason: organisation.suspensionReason,
              })
            : t("organisations.suspendedNoReason")}
        </Banner>
      );
    if (status === TenantResponseStatus.Closed && organisation.closedAt)
      return (
        <Banner
          tone="info"
          title={by("closedOn", organisation.closedAt, organisation.closedBy)}
          action={
            <Button onClick={() => actions.start("reopen", organisation)}>
              {t("organisations.reopen")}
            </Button>
          }
        >
          {organisation.reopenableUntil &&
            t("organisations.closedBannerText", {
              date: date(organisation.reopenableUntil),
            })}
        </Banner>
      );
    if (status === TenantResponseStatus.Purging)
      return (
        <Banner
          tone="destructive"
          title={
            organisation.purgeStartedAt
              ? t("organisations.beingDeletedSince", {
                  date: date(organisation.purgeStartedAt),
                })
              : t("organisations.statusPurging")
          }
        >
          {t("organisations.beingDeletedText")}
        </Banner>
      );
    if (status === TenantResponseStatus.Purged && organisation.purgedAt)
      return (
        <Banner
          tone="info"
          title={t("organisations.deletedOn", {
            date: date(organisation.purgedAt),
          })}
        >
          {t("organisations.deletedText")}
        </Banner>
      );
    return null;
  })();

  const items = [
    { id: "overview", label: t("organisations.overview") },
    ...(gone ? [] : [{ id: "plan", label: t("organisations.plan") }]),
    { id: "activity", label: t("organisations.activity") },
  ];

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <div>
        {/* A link to the list, drawn as the ghost button of the frame: a link, not a button, because it navigates. */}
        <Link href="/" className={buttonVariants({ variant: "ghost" })}>
          <Icon icon={ArrowLeftIcon} size={16} className="rtl:rotate-180" />
          {t("organisations.title")}
        </Link>
      </div>
      <div className="flex flex-col gap-3 tablet:flex-row tablet:items-center tablet:justify-between">
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-page-title">
              <bdi>{name}</bdi>
            </h1>
            <Pill tone={statuses[status].tone}>
              {t(statuses[status].label)}
            </Pill>
            {actionsOf(organisation).length > 0 && (
              <RowMenu
                label={t("organisations.actionsFor", { name })}
                items={actions.itemsFor(organisation)}
              />
            )}
          </div>
          <bdi dir="ltr" className="text-body text-muted-foreground">
            {address}
          </bdi>
        </div>
        {waitsForOwner(organisation) && (
          <Button
            variant="primary"
            onClick={() => actions.start("inviteOwner", organisation)}
          >
            {invitation
              ? t("organisations.inviteOwnerAgain")
              : t("organisations.inviteOwner")}
          </Button>
        )}
      </div>
      {banner}
      <ViewChoice
        label={t("organisations.views")}
        current={shown}
        items={items}
        onValueChange={(next) =>
          void setView(views.find((candidate) => candidate === next) ?? null)
        }
      />
      {shown === "overview" && (
        <Overview organisation={organisation} address={address} />
      )}
      {shown === "plan" && <PlanSection organisation={organisation} />}
      {shown === "activity" && (
        <OrganisationActivity tenantId={organisation.id} />
      )}
      {shown === "overview" && waitsForOwner(organisation) && (
        <p className="text-small text-muted-foreground">
          {t.rich("organisations.ownerlessHint", {
            link: (chunks) => (
              <TextLink href={usersFoundBy(invitation?.email)}>
                {chunks}
              </TextLink>
            ),
          })}
        </p>
      )}
      {actions.dialog}
    </section>
  );
}

/** One row of the Overview card (4.09): the term in its column, the value after it; one above the other on a phone (4.44). */
function Row({
  term,
  children,
  wide = false,
}: {
  term: string;
  children: ReactNode;
  /** A row the phone leaves out, because the page's header already shows it: the address and the status. */
  wide?: boolean;
}) {
  return (
    <div
      className={
        wide
          ? "flex-col gap-0.5 max-tablet:hidden tablet:flex tablet:flex-row tablet:items-center tablet:gap-6 tablet:px-5 tablet:py-3"
          : "flex flex-col gap-0.5 tablet:flex-row tablet:items-center tablet:gap-6 tablet:px-5 tablet:py-3"
      }
    >
      <dt className="text-small text-muted-foreground tablet:w-48 tablet:shrink-0">
        {term}
      </dt>
      <dd className="flex min-w-0 flex-col gap-0.5 text-body">{children}</dd>
    </div>
  );
}

/** A line under a value: the muted, smaller text of the frame ("It never changes."). */
const Note = ({ children }: { children: ReactNode }) => (
  <span className="text-small text-muted-foreground">{children}</span>
);

function Overview({
  organisation,
  address,
}: {
  organisation: TenantDetailResponse;
  address: string;
}) {
  const t = useTranslations();
  const { date, dateTime } = useDates();
  const personName = usePersonName();
  const membersText = useMembersText();
  const plan = useQuery(getGetTenantPlanQueryOptions(organisation.id));
  const gone = isGone(organisation);
  const invitation = waitingInvitation(organisation);
  const members = membersText(organisation, plan.data);
  const { status } = organisation;

  const owner = (() => {
    if (gone) return <EmptyValue />;
    if (organisation.owners.length > 0)
      return organisation.owners.map((person) => (
        <span key={person.userId} className="flex flex-wrap items-center gap-2">
          <span>
            {nameOf(person) && <bdi>{nameOf(person)}</bdi>}
            {nameOf(person) && person.email && " · "}
            {person.email && <bdi dir="ltr">{person.email}</bdi>}
          </span>
          {person.email && (
            <CopyButton
              value={person.email}
              label={t("organisations.copyEmail")}
            />
          )}
        </span>
      ));
    if (waitsForOwner(organisation) && invitation)
      return (
        <>
          <span>
            {t("organisations.invitedEmail", { email: invitation.email })}
          </span>
          {invitation.sentAt && (
            <Note>
              {t("organisations.sent", { date: dateTime(invitation.sentAt) })}
            </Note>
          )}
        </>
      );
    return (
      <span className="text-muted-foreground">
        {t("organisations.noOwner")}
      </span>
    );
  })();

  return (
    <dl className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4 tablet:gap-0 tablet:divide-y tablet:divide-border tablet:p-0">
      <Row term={t("organisations.address")} wide>
        <span className="flex flex-wrap items-center gap-2">
          <bdi dir="ltr">{address}</bdi>
          <CopyButton value={address} label={t("organisations.copyAddress")} />
        </span>
        <Note>{t("organisations.addressNeverChanges")}</Note>
      </Row>
      <Row term={t("organisations.status")} wide>
        <span>
          <Pill tone={statuses[status].tone}>{t(statuses[status].label)}</Pill>
        </span>
      </Row>
      <Row term={t("organisations.owner")}>{owner}</Row>
      <Row term={t("organisations.members")}>
        {gone ? (
          <EmptyValue />
        ) : (
          <>
            <span>{members.sentence}</span>
            {members.seats && <Note>{members.seats}</Note>}
          </>
        )}
      </Row>
      <Row term={t("organisations.plan")}>
        {plan.data ? <bdi>{plan.data.plan.name}</bdi> : <EmptyValue />}
      </Row>
      <Row term={t("organisations.createdTerm")}>
        {organisation.createdBy
          ? t("organisations.createdOnBy", {
              date: date(organisation.createdAt),
              name: personName(organisation.createdBy),
            })
          : date(organisation.createdAt)}
      </Row>
    </dl>
  );
}

function OrganisationActivity({ tenantId }: { tenantId: string }) {
  const t = useTranslations();
  const activity = usePlatformActivity(tenantId);
  return (
    <PlatformActivity
      activity={activity}
      emptyText={t("activity.tenantText")}
    />
  );
}
