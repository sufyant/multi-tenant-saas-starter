"use client";

import {
  InvitationResponseState,
  type ListPageOfTenantListItemResponse,
  ListTenantsSort,
  type TenantListItemResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Status } from "@repo/ui/components/status";
import {
  sortFrom,
  sortOf,
  Table,
  type TableColumn,
} from "@repo/ui/components/table";
import { Time } from "@repo/ui/components/time";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { Initial } from "@/shared/initial";
import {
  actionsOf,
  daysUntil,
  isGone,
  statuses,
  waitingInvitation,
} from "./organisation";
import type { useOrganisationActions } from "./use-organisation-actions";

/** The columns a person can sort by, and the API's sort field of each (K5). */
const sorts = {
  organisation: {
    asc: ListTenantsSort.Name,
    desc: ListTenantsSort.NameDescending,
  },
};

type Row = TenantListItemResponse;

/**
 * The list of organisations (the Main board): Organisation with its slug, Owner, Members, Status and Created. The Owner column
 * counts the Owners and never names one (`ownerCount`); with none, it shows the Owner invitation that waits. A row whose purge has
 * begun has no menu. Created does not sort: ListTenants sorts by name and slug only.
 */
export function OrganisationsTable({
  page,
  sort,
  timeZone,
  actions,
  onSortChange,
  onPageChange,
}: {
  page: ListPageOfTenantListItemResponse;
  sort: string | null;
  timeZone: string | null | undefined;
  actions: ReturnType<typeof useOrganisationActions>;
  onSortChange: (sort: string | null) => void;
  onPageChange: (page: number) => void;
}) {
  const t = useTranslations();
  const now = new Date();
  const locale = t("locale.format");
  const muted = (text: string) => (
    <span className="text-muted-foreground">{text}</span>
  );

  const owner = (row: Row) => {
    if (isGone(row)) return muted("—");
    if (row.ownerCount > 0)
      return t("organisations.ownerCount", { count: row.ownerCount });
    if (row.status !== TenantResponseStatus.Active)
      return muted(t("organisations.noOwner"));
    const invitation = waitingInvitation(row);
    return (
      <span className="flex min-w-0 flex-col items-start">
        <Status tone="info">{t("organisations.waitingForOwner")}</Status>
        {invitation && (
          <span className="text-small text-muted-foreground">
            {invitation.state === InvitationResponseState.Expired
              ? t("organisations.invitationExpired")
              : t.rich("organisations.invitationExpires", {
                  email: () => <bdi dir="ltr">{invitation.email}</bdi>,
                  time: () => (
                    <Time
                      at={invitation.expiresAt}
                      show="relative"
                      locale={locale}
                      timeZone={timeZone}
                    />
                  ),
                })}
          </span>
        )}
      </span>
    );
  };

  const status = (row: Row) => (
    <span className="flex flex-col items-start">
      <Status tone={statuses[row.status].tone}>
        {t(statuses[row.status].label)}
      </Status>
      {row.status === TenantResponseStatus.Closed && row.reopenableUntil && (
        <span className="text-small text-muted-foreground">
          {t("organisations.deletedIn", {
            days: daysUntil(row.reopenableUntil, now),
          })}
        </span>
      )}
    </span>
  );

  const name = (row: Row) => (
    <Link
      href={`/organisations/${row.id}`}
      className="truncate font-medium hover:underline"
    >
      <bdi>{row.name}</bdi>
    </Link>
  );

  const columns: TableColumn<Row>[] = [
    {
      key: "organisation",
      header: t("organisations.organisation"),
      sortable: true,
      render: (row) => (
        <span className="flex min-w-0 items-center gap-3">
          <Initial name={row.name} />
          <span className="flex min-w-0 flex-col">
            {name(row)}
            <bdi
              dir="ltr"
              className="truncate font-mono text-small text-muted-foreground"
            >
              {row.slug}
            </bdi>
          </span>
        </span>
      ),
      cardRender: name,
    },
    {
      key: "owner",
      header: t("organisations.owner"),
      card: "hide",
      render: owner,
    },
    {
      key: "members",
      header: t("organisations.members"),
      align: "end",
      card: "hide",
      render: (row) => (isGone(row) ? muted("—") : row.memberCount),
    },
    {
      key: "status",
      header: t("organisations.status"),
      card: "end",
      render: status,
    },
    {
      key: "created",
      header: t("organisations.createdTerm"),
      secondary: true,
      card: "hide",
      render: (row) => (
        <Time
          at={row.createdAt}
          show="date"
          locale={locale}
          timeZone={timeZone}
        />
      ),
    },
  ];

  return (
    <Table
      columns={columns}
      rows={page.items}
      rowKey="id"
      label={t("organisations.title")}
      actions={(row) =>
        actionsOf(row).length > 0 && (
          <RowMenu
            label={t("organisations.actionsFor", { name: row.name })}
            items={actions.itemsFor(row)}
          />
        )
      }
      actionsLabel={t("organisations.actions")}
      cardLead={(row) => <Initial name={row.name} />}
      rowCount={page.total}
      page={page.page}
      pageSize={page.pageSize}
      onPageChange={onPageChange}
      sort={sortOf(sorts, sort)}
      onSortChange={(next) => onSortChange(sortFrom(sorts, next))}
      pager={{
        previous: t("common.previous"),
        next: t("common.next"),
        page: (current, count) => t("common.page", { page: current, count }),
      }}
    />
  );
}
