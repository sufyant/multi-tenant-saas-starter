"use client";

import {
  getGetMeQueryOptions,
  getListUsersInfiniteQueryOptions,
  problemOf,
  type UserResponse,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Icon, PlusIcon, TrashIcon } from "@repo/ui/components/icon";
import { Label } from "@repo/ui/components/label";
import { ListError } from "@repo/ui/components/list-error";
import { LoadMore } from "@repo/ui/components/load-more";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Table, type TableColumn } from "@repo/ui/components/table";
import { Time } from "@repo/ui/components/time";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@repo/ui/components/ui/tooltip";
import { nextPage, pageSize } from "@repo/ui/lib/pages";
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { userNameOf } from "@/shared/name-of";
import { leaveOf } from "@/shared/problem-screen";
import { systemAdminPage } from "@/shared/system-admins";
import { useDates } from "@/shared/use-dates";
import { useNextPage } from "@/shared/use-next-page";
import { AddDialog } from "./add-dialog";
import { RemoveDialog } from "./remove-dialog";

/** The Root label, whose tooltip says why the row has no action (10.16; the maintainer's decision of 2026-11-04). */
export function RootLabel() {
  const t = useTranslations("systemAdmins");
  return (
    <Tooltip>
      <TooltipTrigger
        // biome-ignore lint/a11y/noNoninteractiveTabindex: the label takes focus so a keyboard can open its tooltip (WCAG 2.2, 1.4.13).
        render={<span tabIndex={0} />}
        className="rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <Label>{t("root")}</Label>
      </TooltipTrigger>
      <TooltipContent>{t("rootHint")}</TooltipContent>
    </Tooltip>
  );
}

/**
 * Settings › System admins (10.01 to 10.16): ListUsers with `isSystemAdmin=true`, each admin with their email and the date added
 * (`systemAdminGrantedAt`), under the frame's text and no Freshness (F59). The list grows with Load more (F9). A root admin has no
 * action: the configuration names them, and the Root label's tooltip says so. The viewer's own row has Remove disabled with its
 * reason (F51). On a phone each admin but the root opens their page (10.14). There is no empty state: there is always a root admin,
 * although one who never signed in is not listed. Every system admin holds every platform permission (the API's A4), so nothing
 * checks one; the API still decides.
 */
export function SystemAdminsPage() {
  const t = useTranslations();
  const { timeZone } = useDates();
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState<UserResponse | null>(null);
  const me = useQuery(getGetMeQueryOptions());
  const list = useInfiniteQuery(
    getListUsersInfiniteQueryOptions(
      { isSystemAdmin: true, pageSize },
      { query: { initialPageParam: 1, getNextPageParam: nextPage } },
    ),
  );
  const next = useNextPage(list, (count) =>
    t("systemAdmins.loadedAdmins", { count }),
  );
  const locale = t("locale.format");
  const nameText = (user: UserResponse) => {
    const name = userNameOf(user, t("users.deletedUser"));
    return user.id === me.data?.id ? t("systemAdmins.nameYou", { name }) : name;
  };

  const columns: TableColumn<UserResponse>[] = [
    {
      key: "name",
      header: t("systemAdmins.name"),
      render: (user) => (
        <span className="flex min-w-0 items-center gap-2">
          <bdi className="truncate">{nameText(user)}</bdi>
          {user.isRootSystemAdmin && <RootLabel />}
        </span>
      ),
      // A phone card opens the admin's page (10.14, 10.15); the root's has nothing to do there.
      cardRender: (user) =>
        user.isRootSystemAdmin ? (
          <span className="flex min-w-0 items-center gap-2">
            <bdi className="truncate">{nameText(user)}</bdi>
            <RootLabel />
          </span>
        ) : (
          <Link href={systemAdminPage(user.id)} className="hover:underline">
            <bdi>{nameText(user)}</bdi>
          </Link>
        ),
    },
    {
      key: "email",
      header: t("systemAdmins.email"),
      width: "w-67.5",
      render: (user) =>
        user.email && (
          <bdi dir="ltr" className="block truncate">
            {user.email}
          </bdi>
        ),
    },
    {
      key: "added",
      header: t("systemAdmins.added"),
      width: "w-79",
      card: "hide",
      render: (user) =>
        user.systemAdminGrantedAt && (
          <Time
            at={user.systemAdminGrantedAt}
            show="date"
            locale={locale}
            timeZone={timeZone}
          />
        ),
    },
  ];

  const actions = (user: UserResponse) => {
    if (user.isRootSystemAdmin) return null;
    const self = user.id === me.data?.id;
    return (
      <RowMenu
        label={t("systemAdmins.actionsFor", {
          name: userNameOf(user, t("users.deletedUser")),
        })}
        items={[
          {
            label: t("systemAdmins.remove"),
            icon: TrashIcon,
            tone: "destructive",
            disabled: self,
            reason: self ? t("systemAdmins.cannotRemoveSelf") : undefined,
            onSelect: () => setRemoving(user),
          },
        ]}
      />
    );
  };

  const body = (() => {
    if (list.isPending)
      return <Skeleton rows={3} label={t("systemAdmins.loading")} />;
    if (!list.data)
      return (
        <ListError
          title={t("systemAdmins.error")}
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    const rows = list.data.pages.flatMap((page) => page.items);
    return (
      <div className="flex flex-col gap-2">
        <Table
          columns={columns}
          rows={rows}
          rowKey="id"
          label={t("systemAdmins.title")}
          actions={actions}
          actionsLabel={t("systemAdmins.actions")}
          rowCount={rows.length}
          page={1}
          pageSize={Math.max(rows.length, 1)}
          pager={{
            previous: t("common.previous"),
            next: t("common.next"),
            page: (current, count) =>
              t("common.page", { page: current, count }),
          }}
        />
        <LoadMore
          hasMore={list.hasNextPage}
          loading={list.isFetchingNextPage}
          onMore={() => void next.more()}
          label={t("systemAdmins.loadMore")}
          loadingLabel={t("systemAdmins.loadingMore")}
          announcement={next.announcement}
          failed={
            next.failed
              ? {
                  text: t("systemAdmins.moreError"),
                  retryLabel: t("common.tryAgain"),
                }
              : undefined
          }
        />
      </div>
    );
  })();

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <div className="flex flex-col gap-4 tablet:flex-row tablet:items-start tablet:justify-between">
        {/* On a phone the top bar names the page (10.14), so its header stays for screen readers. */}
        <div className="sr-only flex min-w-0 flex-col gap-0.5 tablet:not-sr-only">
          <h1 className="text-page-title">{t("systemAdmins.title")}</h1>
          <p className="max-w-[720px] text-body text-muted-foreground">
            {t("systemAdmins.text")}
          </p>
        </div>
        <Button
          variant="primary"
          className="max-tablet:h-control-lg max-tablet:w-full"
          onClick={() => setAdding(true)}
        >
          <Icon icon={PlusIcon} />
          {t("systemAdmins.add")}
        </Button>
      </div>
      {body}
      {adding && <AddDialog onClose={() => setAdding(false)} />}
      {removing && (
        <RemoveDialog user={removing} onClose={() => setRemoving(null)} />
      )}
    </section>
  );
}
