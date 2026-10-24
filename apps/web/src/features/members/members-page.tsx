"use client";

import {
  getGetMeQueryOptions,
  getListInvitationsQueryOptions,
  getListMembersQueryOptions,
  ListInvitationsSort,
  ListMembersSort,
  Permission,
  problemOf,
  schemas,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Combobox } from "@repo/ui/components/combobox";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Freshness } from "@repo/ui/components/freshness";
import { ListError } from "@repo/ui/components/list-error";
import { Search } from "@repo/ui/components/search";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Tabs } from "@repo/ui/components/tabs";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { type ReactNode, useCallback, useState } from "react";
import { PermissionDenied } from "@/shared/permission-denied";
import { leaveOf } from "@/shared/problem-screen";
import { inListOrder } from "@/shared/roles";
import { SettingsNav } from "@/shared/settings-nav";
import { useCan, useTenant } from "@/shared/tenant-context";
import { useTenantRoles } from "@/shared/use-can-act-on";
import { useRoleName } from "@/shared/use-role-name";
import { InvitationsTable } from "./invitations-table";
import { InviteDialog } from "./invite-dialog";
import { MembersTable } from "./members-table";
import { sortValue, type Tab, useListParams } from "./use-list-params";

const allRoles = "all";

/** Settings > Members (the Members boards): the Members tab, and the Invitations tab for whom may read them (F5). */
export function MembersPage() {
  const t = useTranslations();
  const roleName = useRoleName();
  const can = useCan();
  const { tenant } = useTenant();
  const [params, setParams] = useListParams();
  const [inviting, setInviting] = useState(false);
  const me = useQuery(getGetMeQueryOptions());
  const onSearch = useCallback(
    (next: string) => void setParams({ q: next || null, page: null }),
    [setParams],
  );
  const seesInvitations = can(Permission.TenantInvitationsRead);
  const readsMembers = can(Permission.TenantMembersRead);
  // The role filter's choices are the tenant's roles, for whom may read them.
  const readsRoles = can(Permission.TenantRolesRead);
  const roles = useTenantRoles();
  // A person without the Invitations tab who opens ?tab=invitations sees the Members tab.
  const tab: Tab = seesInvitations ? params.tab : "members";
  const { q, roleId, page, sort } = params;
  const search = {
    q: q || undefined,
    roleId: roleId ?? undefined,
    page,
  };

  const members = useQuery(
    getListMembersQueryOptions(
      tenant.id,
      {
        ...search,
        sort: Object.values(ListMembersSort).find((value) => value === sort),
      },
      {
        query: {
          enabled: readsMembers && tab === "members",
          placeholderData: keepPreviousData,
        },
      },
    ),
  );
  const invitations = useQuery(
    getListInvitationsQueryOptions(
      tenant.id,
      {
        ...search,
        sort: Object.values(ListInvitationsSort).find(
          (value) => value === sort,
        ),
      },
      {
        query: {
          enabled: seesInvitations && tab === "invitations",
          placeholderData: keepPreviousData,
        },
      },
    ),
  );
  const list = tab === "members" ? members : invitations;

  if (!readsMembers)
    return (
      <Frame>
        <PermissionDenied page={t("settings.members")} />
      </Frame>
    );

  const filtered = Boolean(q || roleId);
  // The empty state of a filtered list offers its own clear button.
  const emptyFiltered = filtered && list.data?.items.length === 0;
  const clear = () => void setParams({ q: null, roleId: null, page: null });
  const timeZone = me.data?.timeZone;

  const body = (() => {
    if (list.isPending)
      return (
        <Skeleton
          rows={4}
          label={
            tab === "members"
              ? t("members.loadingMembers")
              : t("members.loadingInvitations")
          }
        />
      );
    if (list.isError)
      return (
        <ListError
          title={
            tab === "members"
              ? t("members.membersError")
              : t("members.invitationsError")
          }
          problem={problemOf(list.error)}
          leave={leaveOf(list.error)}
          at={list.errorUpdatedAt}
          onRetry={() => void list.refetch()}
        />
      );
    if (list.data.items.length === 0 && filtered)
      return (
        <EmptyState
          kind="no-results"
          title={
            q
              ? t(
                  tab === "members"
                    ? "members.noMembersMatch"
                    : "members.noInvitationsMatch",
                  { q },
                )
              : t(
                  tab === "members"
                    ? "members.noMembersMatchFilter"
                    : "members.noInvitationsMatchFilter",
                )
          }
          description={
            tab === "members"
              ? t("members.noMembersMatchText")
              : t("members.noInvitationsMatchText")
          }
          action={
            <Button onClick={clear}>
              {q ? t("members.clearSearch") : t("members.clearFilter")}
            </Button>
          }
        />
      );
    if (list.data.items.length === 0 && tab === "invitations")
      return (
        <EmptyState
          title={t("members.noInvitations")}
          description={t("members.noInvitationsText")}
          action={
            can(Permission.TenantInvitationsCreate) && (
              <Button variant="primary" onClick={() => setInviting(true)}>
                {t("members.invite")}
              </Button>
            )
          }
        />
      );
    const table = {
      sort,
      timeZone,
      onSortChange: (next: string | null) =>
        void setParams({ sort: sortValue(next), page: null }),
      onPageChange: (next: number) => void setParams({ page: next }),
    };
    return tab === "members" && members.data ? (
      <MembersTable page={members.data} meId={me.data?.id} {...table} />
    ) : invitations.data ? (
      <InvitationsTable page={invitations.data} {...table} />
    ) : null;
  })();

  return (
    <Frame>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-page-title">{t("members.title")}</h1>
          {list.dataUpdatedAt > 0 && (
            <Freshness
              at={new Date(list.dataUpdatedAt)}
              locale={t("locale.format")}
              timeZone={timeZone}
              label={t("common.updated")}
            />
          )}
        </div>
        {can(Permission.TenantInvitationsCreate) && (
          <Button variant="primary" onClick={() => setInviting(true)}>
            {t("members.invite")}
          </Button>
        )}
      </div>
      <div className="flex flex-wrap items-end justify-between gap-4">
        {seesInvitations && (
          <Tabs
            label={t("members.tabs")}
            current={tab}
            items={[
              { id: "members", label: t("members.tabMembers") },
              { id: "invitations", label: t("members.tabInvitations") },
            ]}
            onValueChange={(next) =>
              void setParams({
                tab: next === "invitations" ? "invitations" : null,
                q: null,
                roleId: null,
                page: null,
                sort: null,
              })
            }
          />
        )}
        <div className="flex flex-1 flex-wrap items-end justify-end gap-2">
          {readsRoles && (
            <Combobox
              variant="compact"
              label={t("members.roleFilter")}
              value={roleId ?? allRoles}
              options={[
                { id: allRoles, label: t("members.allRoles") },
                ...inListOrder(roles.data?.items ?? []).map((choice) => ({
                  id: choice.id,
                  label: t("members.filteredBy", { role: roleName(choice) }),
                })),
              ]}
              onValueChange={(id) =>
                void setParams({
                  roleId:
                    roles.data?.items.find((choice) => choice.id === id)?.id ??
                    null,
                  page: null,
                })
              }
              searchPlaceholder={t("members.roleSearch")}
              emptyLabel={t("members.roleEmpty")}
            />
          )}
          {roleId && !emptyFiltered && (
            <Button
              variant="ghost"
              onClick={() => void setParams({ roleId: null, page: null })}
            >
              {t("members.clearFilter")}
            </Button>
          )}
          <Search
            key={tab}
            value={q}
            label={
              tab === "members"
                ? t("members.searchMembers")
                : t("members.searchInvitations")
            }
            maxLength={schemas.listMembersQueryQMax}
            onSearch={onSearch}
          />
        </div>
      </div>
      {body}
      {inviting && <InviteDialog onClose={() => setInviting(false)} />}
    </Frame>
  );
}

function Frame({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="members" />
      <section className="flex min-w-0 flex-1 flex-col gap-4">
        {children}
      </section>
    </div>
  );
}
