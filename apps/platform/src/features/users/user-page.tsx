"use client";

import {
  ErrorCode,
  getGetUserQueryOptions,
  problemOf,
  schemas,
  type UserDetailResponse,
  type UserMembershipResponse,
  UserMembershipResponseTenantStatus,
  UserResponseStatus,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { CopyButton } from "@repo/ui/components/copy-button";
import { Label } from "@repo/ui/components/label";
import { Pill, type PillTone } from "@repo/ui/components/pill";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Time } from "@repo/ui/components/time";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { BackLink } from "@/shared/back-link";
import { DetailList } from "@/shared/detail-list";
import { nameOf, userNameOf } from "@/shared/name-of";
import { NotFoundPage } from "@/shared/not-found-page";
import { ProblemScreen } from "@/shared/problem-screen";
import { useDates } from "@/shared/use-dates";
import { MakeOwnerDialog } from "./make-owner-dialog";
import { isOwnerMembership } from "./owner-membership";
import { ReactivateDialog } from "./reactivate-dialog";
import { useRoleName } from "./use-role-name";
import { useUserActions } from "./use-user-actions";
import { actionsOf } from "./user";
import { UserStatus } from "./users-table";

/** An organisation that is not active shows its status beside the role (5.05); an active one shows none. */
const tenantStatuses = {
  [UserMembershipResponseTenantStatus.Suspended]: {
    label: "organisations.statusSuspended",
    tone: "warning",
  },
  [UserMembershipResponseTenantStatus.Closed]: {
    label: "organisations.statusClosed",
    tone: "neutral",
  },
  [UserMembershipResponseTenantStatus.Purging]: {
    label: "organisations.statusPurging",
    tone: "destructive",
  },
} as const satisfies Record<
  Exclude<
    UserMembershipResponseTenantStatus,
    typeof UserMembershipResponseTenantStatus.Active
  >,
  { label: string; tone: PillTone }
>;

/**
 * One user (5.05 to 5.15, 5.17): Back to Users, the name with its status, the System admin label and the actions, the suspension's
 * banner, the details and the organisations with Make Owner. A deleted user shows the deleted state, without actions.
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
  const { timeZone } = useDates();
  const actions = useUserActions();
  const locale = t("locale.format");
  const name = userNameOf(user, t("users.deletedUser"));

  // A deleted user has no email (A8), so their page has no Email row.
  const rows: { term: string; value: ReactNode }[] = [
    ...(user.email
      ? [
          {
            term: t("users.emailTerm"),
            value: (
              <>
                <bdi dir="ltr">{user.email}</bdi>
                <CopyButton value={user.email} label={t("users.copyEmail")} />
              </>
            ),
          },
        ]
      : []),
    { term: t("users.statusTerm"), value: <UserStatus status={user.status} /> },
    {
      term: t("users.signedUpTerm"),
      value: (
        <Time
          at={user.createdAt}
          show="date"
          locale={locale}
          timeZone={timeZone}
        />
      ),
    },
    { term: t("users.organisationsTerm"), value: user.membershipCount },
  ];

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <BackLink href="/users" label={t("users.backToUsers")} />
      <div className="flex min-w-0 flex-col gap-0.5">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-page-title">
            <bdi>{name}</bdi>
          </h1>
          {user.isSystemAdmin && <Label>{t("users.systemAdmin")}</Label>}
          {user.isRootSystemAdmin && <Label>{t("systemAdmins.root")}</Label>}
          <UserStatus status={user.status} />
          {actionsOf(user).length > 0 && (
            <RowMenu
              label={t("users.actionsFor", { name })}
              items={actions.itemsFor(user)}
            />
          )}
        </div>
        {user.email && (
          <bdi dir="ltr" className="text-small text-muted-foreground">
            {user.email}
          </bdi>
        )}
      </div>
      <Suspension user={user} />
      <DetailList rows={rows} />
      <Memberships user={user} />
      {actions.dialog}
    </section>
  );
}

/**
 * The suspension's banner (5.07): when, by whom and why, with Reactivate. A "by" or a reason that the API does not know (an event
 * before 37b-backend) is left out; a deleted admin is "Deleted user". The reason is only the platform's; the user never sees it.
 */
function Suspension({ user }: { user: UserDetailResponse }) {
  const t = useTranslations("users");
  const { date } = useDates();
  const [reactivating, setReactivating] = useState(false);
  if (user.status !== UserResponseStatus.Suspended || !user.suspendedAt)
    return null;
  const by =
    user.suspendedBy &&
    ((!user.suspendedBy.deleted && nameOf(user.suspendedBy)) ||
      t("deletedUser"));
  return (
    <>
      <Banner
        tone="warning"
        title={
          by
            ? t("suspendedOnBy", { date: date(user.suspendedAt), name: by })
            : t("suspendedOn", { date: date(user.suspendedAt) })
        }
        action={
          <Button onClick={() => setReactivating(true)}>
            {t("reactivate")}
          </Button>
        }
      >
        {user.suspensionReason
          ? t("suspendedReason", { reason: user.suspensionReason })
          : t("suspendedNoReason")}
      </Banner>
      {reactivating && (
        <ReactivateDialog user={user} onClose={() => setReactivating(false)} />
      )}
    </>
  );
}

/**
 * The user's organisations (5.05, 5.08): each by its name, a status that is not active, the role, and Make Owner where the user is
 * not an Owner yet. Without one, "In no organisation.".
 */
function Memberships({ user }: { user: UserDetailResponse }) {
  const t = useTranslations();
  const roleName = useRoleName();
  const [making, setMaking] = useState<UserMembershipResponse | null>(null);

  if (user.memberships.length === 0)
    return (
      <p className="rounded-lg border border-border bg-card px-4 py-3.5 text-body text-muted-foreground">
        {t("users.noMemberships")}
      </p>
    );
  return (
    <>
      <ul
        aria-label={t("users.organisationsTerm")}
        className="flex flex-col overflow-hidden rounded-lg border border-border bg-card"
      >
        {user.memberships.map((membership) => {
          const status =
            membership.tenantStatus ===
            UserMembershipResponseTenantStatus.Active
              ? null
              : tenantStatuses[membership.tenantStatus];
          return (
            <li
              key={membership.tenantId}
              className="flex min-h-row-height flex-wrap items-center gap-2 border-b border-border px-4 py-1.5 last:border-b-0"
            >
              <Link
                href={`/organisations/${membership.tenantId}`}
                className="min-w-0 flex-1 truncate text-body hover:underline"
              >
                <bdi>{membership.tenantName}</bdi>
              </Link>
              {status && <Pill tone={status.tone}>{t(status.label)}</Pill>}
              {/* A built-in role by its translated name; a custom role as the organisation stored it, isolated from the page's direction. */}
              <Label>
                <bdi>{roleName(membership.role)}</bdi>
              </Label>
              {!isOwnerMembership(membership) && (
                <Button
                  aria-label={t("users.makeOwnerOf", {
                    organisation: membership.tenantName,
                  })}
                  onClick={() => setMaking(membership)}
                >
                  {t("users.makeOwner")}
                </Button>
              )}
            </li>
          );
        })}
      </ul>
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
