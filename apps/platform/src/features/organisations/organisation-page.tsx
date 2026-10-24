"use client";

import {
  ErrorCode,
  getGetTenantQueryOptions,
  InvitationResponseState,
  schemas,
  type TenantDetailResponse,
  type TenantOwnerResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Status } from "@repo/ui/components/status";
import { Tabs } from "@repo/ui/components/tabs";
import { Time } from "@repo/ui/components/time";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { parseAsStringLiteral, useQueryState } from "nuqs";
import type { ReactNode } from "react";
import { DetailList } from "@/shared/detail-list";
import { Initial } from "@/shared/initial";
import { NotFoundPage } from "@/shared/not-found-page";
import {
  PlatformActivity,
  usePlatformActivity,
} from "@/shared/platform-activity";
import { problemOf } from "@/shared/problem";
import { ProblemScreen } from "@/shared/problem-screen";
import { useDates } from "@/shared/use-dates";
import {
  actionsOf,
  statuses,
  waitingInvitation,
  waitsForOwner,
} from "./organisation";
import { useOrganisationActions } from "./use-organisation-actions";

/** The Users list, searched by the invited email: an Owner who joined as a member is made an Owner from their user page. */
const usersFoundBy = (email: string | undefined) =>
  email ? `/users?${new URLSearchParams({ q: email })}` : "/users";

/** The tabs of an organisation (the OrgDetail and OrgActivity boards). The tab lives in the URL, as every tab of the app (F9). */
const views = ["overview", "activity"] as const;
const viewParser = parseAsStringLiteral(views).withDefault("overview");

const nameOf = (owner: TenantOwnerResponse) =>
  [owner.firstName, owner.lastName].filter(Boolean).join(" ") || null;

/**
 * One organisation (the OrgDetail, OrgOwnerless, OrgClosed and OrgPurged boards): the header with the actions, the banner of its
 * state, and two tabs. Overview holds its details from GetTenant; Activity holds the platform's entries about it (P3.2), also for a
 * deleted organisation. Every date comes from the answer, never a fixed period.
 */
export function OrganisationPage({ tenantId }: { tenantId: string }) {
  const known =
    schemas.GetTenantParams.shape.tenantId.safeParse(tenantId).success;
  const tenant = useQuery(
    getGetTenantQueryOptions(tenantId, { query: { enabled: known } }),
  );
  if (!known || problemOf(tenant.error)?.code === ErrorCode.TenantNotFound)
    return <NotFoundPage />;
  if (tenant.error) return <ProblemScreen error={tenant.error} />;
  if (!tenant.data) return <Loading />;
  return <Overview organisation={tenant.data} />;
}

function Loading() {
  const t = useTranslations("common");
  return <Skeleton rows={4} label={t("loading")} />;
}

function Overview({ organisation }: { organisation: TenantDetailResponse }) {
  const t = useTranslations();
  const { date, timeZone } = useDates();
  const actions = useOrganisationActions();
  const locale = t("locale.format");
  const { name, status } = organisation;
  const [view, setView] = useQueryState(
    "tab",
    viewParser.withOptions({ history: "push" }),
  );
  const items = actions.itemsFor(organisation);
  const invitation = waitingInvitation(organisation);
  const email = (address: string) => <bdi dir="ltr">{address}</bdi>;

  const banner = (() => {
    if (waitsForOwner(organisation)) {
      const expired = invitation?.state === InvitationResponseState.Expired;
      return (
        <Banner
          tone={expired ? "warning" : "info"}
          title={t("organisations.waitingTitle")}
          action={
            <Button
              variant="primary"
              onClick={() => actions.start("inviteOwner", organisation)}
            >
              {t("organisations.inviteOwner")}
            </Button>
          }
        >
          {invitation &&
            t.rich(
              expired
                ? "organisations.waitingExpired"
                : "organisations.waitingPending",
              {
                email: () => email(invitation.email),
                date: date(invitation.expiresAt),
              },
            )}
        </Banner>
      );
    }
    if (status === TenantResponseStatus.Suspended)
      return (
        <Banner tone="warning" title={t("organisations.suspendedTitle")}>
          {organisation.suspensionReason &&
            t("organisations.suspendedText", {
              reason: organisation.suspensionReason,
            })}
        </Banner>
      );
    if (
      status === TenantResponseStatus.Closed &&
      organisation.closedAt &&
      organisation.reopenableUntil
    ) {
      const warning = organisation.purgeWarningAt;
      return (
        <Banner
          tone="info"
          title={t("organisations.closedTitle", {
            closedAt: date(organisation.closedAt),
            reopenableUntil: date(organisation.reopenableUntil),
          })}
          action={
            <Button onClick={() => actions.start("reopen", organisation)}>
              {t("organisations.reopen")}
            </Button>
          }
        >
          {warning &&
            t(
              new Date(warning) > new Date()
                ? "organisations.warnedFuture"
                : "organisations.warnedPast",
              { date: date(warning) },
            )}
        </Banner>
      );
    }
    if (status === TenantResponseStatus.Purging)
      return (
        <Banner tone="info" title={t("organisations.beingDeleted", { name })} />
      );
    return null;
  })();

  const owners: ReactNode = (() => {
    if (organisation.owners.length > 0)
      return (
        <ul className="flex flex-col gap-1">
          {organisation.owners.map((owner) => (
            <li key={owner.userId} className="flex flex-wrap gap-2">
              {nameOf(owner) && <bdi>{nameOf(owner)}</bdi>}
              {owner.email && (
                <span className="text-muted-foreground">
                  {email(owner.email)}
                </span>
              )}
            </li>
          ))}
        </ul>
      );
    if (!waitsForOwner(organisation))
      return (
        <span className="text-muted-foreground">
          {t("organisations.noOwner")}
        </span>
      );
    return (
      <>
        <Status tone="info">{t("organisations.waitingForOwner")}</Status>
        {invitation && (
          <span className="text-muted-foreground">
            {invitation.state === InvitationResponseState.Expired
              ? t("organisations.ownerExpired")
              : t.rich("organisations.ownerPending", {
                  email: () => email(invitation.email),
                  date: date(invitation.expiresAt),
                })}
          </span>
        )}
      </>
    );
  })();

  const rows: { term: string; value: ReactNode }[] = [
    {
      term: t("organisations.ownersTerm", {
        count: Math.max(organisation.ownerCount, 1),
      }),
      value: owners,
    },
    { term: t("organisations.members"), value: organisation.memberCount },
  ];
  if (organisation.closedAt)
    rows.push({
      term: t("organisations.closedTerm"),
      value: (
        <Time
          at={organisation.closedAt}
          show="datetime"
          locale={locale}
          timeZone={timeZone}
        />
      ),
    });
  if (
    organisation.suspensionReason &&
    status === TenantResponseStatus.Suspended
  )
    rows.push({
      term: t("organisations.reasonTerm"),
      value: organisation.suspensionReason,
    });

  return (
    <section className="flex min-w-0 max-w-[1160px] flex-col gap-4">
      {banner}
      <div className="flex items-start gap-3">
        <Initial name={name} />
        <div className="flex min-w-0 flex-1 flex-col">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-page-title">
              <bdi>{name}</bdi>
            </h1>
            <Status tone={statuses[status].tone}>
              {t(statuses[status].label)}
            </Status>
          </div>
          <bdi dir="ltr" className="font-mono text-small text-muted-foreground">
            {organisation.slug}
          </bdi>
        </div>
        {actionsOf(organisation).length > 0 && (
          <RowMenu
            label={t("organisations.actionsFor", { name })}
            items={items}
          />
        )}
      </div>
      <Tabs
        label={t("organisations.organisation")}
        current={view}
        items={[
          { id: "overview", label: t("organisations.overview") },
          { id: "activity", label: t("organisations.activity") },
        ]}
        onValueChange={(next) =>
          void setView(views.find((candidate) => candidate === next) ?? null)
        }
      />
      {view === "activity" ? (
        <OrganisationActivity tenantId={organisation.id} />
      ) : status === TenantResponseStatus.Purged ? (
        <div className="flex max-w-[640px] flex-col gap-2 rounded-lg border border-border bg-card px-5 py-4 text-body">
          {organisation.purgedAt && (
            <p>
              {t("organisations.purgedOn", {
                date: date(organisation.purgedAt),
              })}
            </p>
          )}
          <p className="text-muted-foreground">
            {t("organisations.purgedText")}
          </p>
        </div>
      ) : (
        <DetailList rows={rows} />
      )}
      {view === "overview" && waitsForOwner(organisation) && (
        <p className="text-small text-muted-foreground">
          {t.rich("organisations.ownerlessHint", {
            link: (chunks) => (
              <Link
                href={usersFoundBy(invitation?.email)}
                className="text-primary underline-offset-4 hover:underline"
              >
                {chunks}
              </Link>
            ),
          })}
        </p>
      )}
      {actions.dialog}
    </section>
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
