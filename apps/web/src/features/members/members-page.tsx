"use client";

import {
  getGetMeQueryOptions,
  getListInvitationsQueryOptions,
  getListMembersQueryOptions,
  ListInvitationsSort,
  ListMembersSort,
  MemberRole,
  Permission,
  schemas,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Combobox } from "@repo/ui/components/combobox";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Freshness } from "@repo/ui/components/freshness";
import { Skeleton } from "@repo/ui/components/skeleton";
import { Tabs } from "@repo/ui/components/tabs";
import { Input as InputBase } from "@repo/ui/components/ui/input";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { type ReactNode, useCallback, useEffect, useState } from "react";
import { PermissionDenied } from "@/shared/permission-denied";
import { SettingsNav } from "@/shared/settings-nav";
import { useCan, useTenant } from "@/shared/tenant-context";
import { InvitationsTable } from "./invitations-table";
import { InviteDialog } from "./invite-dialog";
import { ListError } from "./list-error";
import { MembersTable } from "./members-table";
import {
  searchDelay,
  sortValue,
  type Tab,
  useListParams,
} from "./use-list-params";

const allRoles = "all";

/** Settings > Members (the Members boards): the Members tab, and the Invitations tab for whom may read them (F5). */
export function MembersPage() {
  const t = useTranslations();
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
  // A person without the Invitations tab who opens ?tab=invitations sees the Members tab.
  const tab: Tab = seesInvitations ? params.tab : "members";
  const { q, role, page, sort } = params;
  const search = {
    q: q || undefined,
    role: role ?? undefined,
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

  const filtered = Boolean(q || role);
  const clear = () => void setParams({ q: null, role: null, page: null });
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
          error={list.error}
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
                role: null,
                page: null,
                sort: null,
              })
            }
          />
        )}
        <div className="flex flex-1 flex-wrap items-end justify-end gap-2">
          <Combobox
            variant="compact"
            label={t("members.roleFilter")}
            value={role ?? allRoles}
            options={[
              { id: allRoles, label: t("members.allRoles") },
              ...Object.values(MemberRole).map((choice) => ({
                id: choice,
                label: t(`shell.roles.${choice}`),
              })),
            ]}
            onValueChange={(id) =>
              void setParams({
                role:
                  Object.values(MemberRole).find((choice) => choice === id) ??
                  null,
                page: null,
              })
            }
            searchPlaceholder={t("members.roleSearch")}
            emptyLabel={t("members.roleEmpty")}
          />
          <Search
            key={tab}
            value={q}
            label={
              tab === "members"
                ? t("members.searchMembers")
                : t("members.searchInvitations")
            }
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

/** The search field: it sends the text 300 ms after the last key, and follows the URL on back and forward. */
function Search({
  value,
  label,
  onSearch,
}: {
  value: string;
  label: string;
  onSearch: (q: string) => void;
}) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  useEffect(() => {
    if (draft === value) return;
    const timer = setTimeout(() => onSearch(draft), searchDelay);
    return () => clearTimeout(timer);
  }, [draft, value, onSearch]);
  return (
    <InputBase
      type="search"
      aria-label={label}
      placeholder={label}
      maxLength={schemas.listMembersQueryQMax}
      value={draft}
      onChange={(event) => setDraft(event.target.value)}
      className="h-control-md w-full rounded-md bg-card px-3 text-body pointer-coarse:h-control-lg tablet:w-64"
    />
  );
}
