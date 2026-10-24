"use client";

import {
  ErrorCode,
  getGetUserQueryOptions,
  schemas,
  type UserDetailResponse,
  type UserMembershipResponse,
  UserResponseStatus,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Status } from "@repo/ui/components/status";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { Time } from "@repo/ui/components/time";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { Avatar } from "@/shared/avatar";
import { DetailList } from "@/shared/detail-list";
import { Initial } from "@/shared/initial";
import { nameOf } from "@/shared/name-of";
import { NotFoundPage } from "@/shared/not-found-page";
import { problemOf } from "@/shared/problem";
import { ProblemScreen } from "@/shared/problem-screen";
import { useDates } from "@/shared/use-dates";
import { MakeOwnerDialog } from "./make-owner-dialog";
import { isOwnerMembership } from "./owner-membership";
import { useRoleName } from "./use-role-name";
import { useUserActions } from "./use-user-actions";
import { actionsOf, displayNameOf, statuses } from "./user";
import { AdminTags } from "./users-table";

/**
 * One user (the UserDetail, ReactivateUser and DeleteUser boards): the header with the System admin badge and the actions, the
 * suspension's banner, the details and the memberships with Make Owner. A deleted user shows the deleted state, without actions.
 */
export function UserPage({ userId }: { userId: string }) {
  const known = schemas.GetUserParams.shape.userId.safeParse(userId).success;
  const user = useQuery(
    getGetUserQueryOptions(userId, { query: { enabled: known } }),
  );
  if (!known || problemOf(user.error)?.code === ErrorCode.UserNotFound)
    return <NotFoundPage />;
  if (user.error) return <ProblemScreen error={user.error} />;
  if (!user.data) return <Loading />;
  return <Detail user={user.data} />;
}

function Loading() {
  const t = useTranslations("common");
  return <Skeleton rows={4} label={t("loading")} />;
}

function Detail({ user }: { user: UserDetailResponse }) {
  const t = useTranslations();
  const { date, timeZone } = useDates();
  const actions = useUserActions();
  const locale = t("locale.format");
  const deletedUser = t("users.deletedUser");
  const name = displayNameOf(user, deletedUser);
  const suspended = user.status === UserResponseStatus.Suspended;
  const suspender =
    (user.suspendedBy &&
      !user.suspendedBy.deleted &&
      nameOf(user.suspendedBy)) ||
    deletedUser;

  const rows: { term: string; value: ReactNode }[] = [
    { term: t("users.organisationsTerm"), value: user.membershipCount },
    {
      term: t("users.createdTerm"),
      value: (
        <Time
          at={user.createdAt}
          show="date"
          locale={locale}
          timeZone={timeZone}
        />
      ),
    },
  ];
  if (suspended && user.suspendedAt)
    rows.push(
      {
        term: t("users.suspendedTerm"),
        value: (
          <Time
            at={user.suspendedAt}
            show="datetime"
            locale={locale}
            timeZone={timeZone}
          />
        ),
      },
      { term: t("users.suspendedByTerm"), value: <bdi>{suspender}</bdi> },
    );

  return (
    <section className="flex min-w-0 max-w-[1160px] flex-col gap-5">
      {suspended && user.suspendedAt && (
        <Banner
          tone="warning"
          title={t("users.suspendedTitle", {
            date: date(user.suspendedAt),
            name: suspender,
          })}
        >
          {t("users.suspendedText")}
        </Banner>
      )}
      <div className="flex items-center gap-3.5">
        <Avatar user={user} className="size-11" />
        <div className="flex min-w-0 flex-1 flex-col">
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-page-title">
              <bdi>{name}</bdi>
            </h1>
            <Status tone={statuses[user.status].tone}>
              {t(statuses[user.status].label)}
            </Status>
            <AdminTags user={user} linked />
          </div>
          {user.email && (
            <bdi dir="ltr" className="text-muted-foreground">
              {user.email}
            </bdi>
          )}
        </div>
        {actionsOf(user).length > 0 && (
          <RowMenu
            label={t("users.actionsFor", { name })}
            items={actions.itemsFor(user)}
          />
        )}
      </div>
      <DetailList rows={rows} />
      <h2 className="text-section-title">{t("users.memberships")}</h2>
      {user.memberships.length > 0 ? (
        <Memberships user={user} timeZone={timeZone} />
      ) : (
        <p className="text-muted-foreground">{t("users.noMemberships")}</p>
      )}
      {actions.dialog}
    </section>
  );
}

/** The user's memberships (the UserDetail board): the organisation, its slug, the role and when they joined, each with Make Owner. */
function Memberships({
  user,
  timeZone,
}: {
  user: UserDetailResponse;
  timeZone: string | null | undefined;
}) {
  const t = useTranslations();
  const roleName = useRoleName();
  const locale = t("locale.format");
  const [making, setMaking] = useState<UserMembershipResponse | null>(null);

  const organisation = (membership: UserMembershipResponse) => (
    <Link
      href={`/organisations/${membership.tenantId}`}
      className="truncate font-medium hover:underline"
    >
      <bdi>{membership.tenantName}</bdi>
    </Link>
  );

  const columns: TableColumn<UserMembershipResponse>[] = [
    {
      key: "organisation",
      header: t("users.organisation"),
      render: (membership) => (
        <span className="flex min-w-0 items-center gap-3">
          <Initial name={membership.tenantName} />
          <span className="flex min-w-0 flex-col">
            {organisation(membership)}
            <bdi
              dir="ltr"
              className="truncate font-mono text-small text-muted-foreground"
            >
              {membership.tenantSlug}
            </bdi>
          </span>
        </span>
      ),
      cardRender: organisation,
    },
    {
      key: "role",
      header: t("users.role"),
      card: "end",
      // A built-in role by its translated name; a custom role as the organisation stored it, isolated from the page's direction.
      render: (membership) => <bdi>{roleName(membership.role)}</bdi>,
    },
    {
      key: "joined",
      header: t("users.joined"),
      secondary: true,
      card: "hide",
      render: (membership) => (
        <Time
          at={membership.joinedAt}
          show="date"
          locale={locale}
          timeZone={timeZone}
        />
      ),
    },
  ];

  return (
    <>
      <Table
        columns={columns}
        rows={user.memberships}
        rowKey="tenantId"
        label={t("users.memberships")}
        actions={(membership) => (
          <RowMenu
            label={t("users.actionsFor", { name: membership.tenantName })}
            items={[
              {
                label: t("users.makeOwner"),
                disabled: isOwnerMembership(membership),
                reason: isOwnerMembership(membership)
                  ? t("users.alreadyOwner")
                  : undefined,
                onSelect: () => setMaking(membership),
              },
            ]}
          />
        )}
        actionsLabel={t("users.actions")}
        cardLead={(membership) => <Initial name={membership.tenantName} />}
        rowCount={user.memberships.length}
        page={1}
        pageSize={user.memberships.length}
        pager={{
          previous: t("common.previous"),
          next: t("common.next"),
          page: (current, count) => t("common.page", { page: current, count }),
        }}
      />
      {making && (
        <MakeOwnerDialog
          user={user}
          membership={making}
          onClose={() => setMaking(null)}
        />
      )}
    </>
  );
}
