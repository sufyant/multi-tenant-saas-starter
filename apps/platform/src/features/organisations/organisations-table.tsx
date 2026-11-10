"use client";

import { ListTenantsSort, type TenantListItemResponse } from "@repo/api-client";
import { EmptyValue } from "@repo/ui/components/empty-value";
import { Pill } from "@repo/ui/components/pill";
import { RowMenu } from "@repo/ui/components/row-menu";
import {
  sortFrom,
  sortOf,
  Table,
  type TableColumn,
} from "@repo/ui/components/table";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { actionsOf, isGone, statuses, waitsForOwner } from "./organisation";
import type { useOrganisationActions } from "./use-organisation-actions";

/** The columns a person can sort by, and the API's sort field of each (K5). */
const sorts = {
  name: {
    asc: ListTenantsSort.Name,
    desc: ListTenantsSort.NameDescending,
  },
};

type Row = TenantListItemResponse;

/**
 * The list of organisations (4.01): Name, Status, Address and Members, with the frame's widths. Members counts the pending
 * invitations too ("10 members · 2 invited"). The phone card names the plan with the members (4.43). A row whose purge has begun has
 * no menu and counts nothing (K4). Every row the pages loaded is one page of the table (F10).
 */
export function OrganisationsTable({
  rows,
  sort,
  actions,
  onSortChange,
}: {
  rows: Row[];
  sort: string | null;
  actions: ReturnType<typeof useOrganisationActions>;
  onSortChange: (sort: string | null) => void;
}) {
  const t = useTranslations();

  const members = (row: Row) => {
    const count =
      row.memberCount > 0
        ? t("organisations.memberCount", { count: row.memberCount })
        : t("organisations.noMembersYet");
    if (row.pendingInvitationCount === 0) return count;
    return t("organisations.membersAndInvited", {
      members: count,
      count: row.pendingInvitationCount,
    });
  };

  const columns: TableColumn<Row>[] = [
    {
      key: "name",
      header: t("organisations.name"),
      sortable: true,
      render: (row) => (
        <Link
          href={`/organisations/${row.id}`}
          className="truncate hover:underline"
        >
          <bdi>{row.name}</bdi>
        </Link>
      ),
    },
    {
      key: "status",
      header: t("organisations.status"),
      width: "w-38.5",
      card: "end",
      render: (row) => (
        <Pill tone={statuses[row.status].tone}>
          {t(statuses[row.status].label)}
        </Pill>
      ),
    },
    {
      key: "slug",
      header: t("organisations.address"),
      width: "w-53.5",
      card: "hide",
      render: (row) => (
        <bdi dir="ltr" className="truncate text-small">
          {row.slug}
        </bdi>
      ),
    },
    {
      key: "members",
      header: t("organisations.members"),
      width: "w-43.5",
      render: (row) =>
        isGone(row) ? (
          <EmptyValue />
        ) : (
          <span className="text-small">{members(row)}</span>
        ),
      cardRender: (row) => {
        const plan = <bdi>{row.plan.name}</bdi>;
        if (isGone(row)) return plan;
        return (
          <>
            {plan}
            {" · "}
            {waitsForOwner(row)
              ? t("organisations.waitingForOwnerLower")
              : t("organisations.memberCount", { count: row.memberCount })}
          </>
        );
      },
    },
  ];

  return (
    <Table
      columns={columns}
      rows={rows}
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
      rowCount={rows.length}
      page={1}
      pageSize={Math.max(rows.length, 1)}
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
