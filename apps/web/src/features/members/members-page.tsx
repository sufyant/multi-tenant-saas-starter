"use client";

import {
  actionFor,
  getGetMeQueryOptions,
  getListInvitationsQueryOptions,
  getListMembersQueryOptions,
  LimitKey,
  ListInvitationsSort,
  ListMembersSort,
  Permission,
  ProblemDetailsError,
  problemOf,
  schemas,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Combobox } from "@repo/ui/components/combobox";
import { EmptyState } from "@repo/ui/components/empty-state";
import {
  Icon,
  PlusIcon,
  SearchXIcon,
  UserPlusIcon,
} from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { RowMenu } from "@repo/ui/components/row-menu";
import { Search } from "@repo/ui/components/search";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { Tabs } from "@repo/ui/components/tabs";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import {
  keepPreviousData,
  useInfiniteQuery,
  useQuery,
} from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useQueryState } from "nuqs";
import { type ReactNode, useCallback, useEffect, useId, useState } from "react";
import { PermissionDenied } from "@/shared/permission-denied";
import { usePlan } from "@/shared/plan";
import { PlanBanner } from "@/shared/plan-banner";
import { leaveOf } from "@/shared/problem-screen";
import { inListOrder } from "@/shared/roles";
import { useCan, useTenant } from "@/shared/tenant-context";
import { useTenantRoles } from "@/shared/use-can-act-on";
import { usePlanReason } from "@/shared/use-plan-reason";
import { useRoleName } from "@/shared/use-role-name";
import { ExportMembersDialog } from "./export-members-dialog";
import { InvitationsTable } from "./invitations-table";
import { InviteDialog } from "./invite-dialog";
import { invitationPagesOptions, memberPagesOptions } from "./list-pages";
import { LoadMore } from "./load-more";
import { MembersTable } from "./members-table";
import { NoFreeSeatDialog } from "./no-free-seat-dialog";
import { TableSkeleton } from "./table-skeleton";
import { sortValue, type Tab, useListParams } from "./use-list-params";

const allRoles = "all";

/** The rows of every page loaded so far. */
const rowsIn = (data: { pages: { items: unknown[] }[] } | undefined) =>
  data?.pages.reduce((count, page) => count + page.items.length, 0);

/**
 * Settings > Members (8.01 to 8.07): the Members tab, and the Invitations tab for whom may read them (F5), each with its total
 * without a filter. A list grows with Load more (F9). The plan (F56, through `usePlan`): "Invite member" stays enabled and opens
 * "No free seat" at and over the limit; the banner between the header and the tabs says the seats, and over the limit offers
 * "View invitations" while one is pending. A viewer who may not invite sees no banner. The More menu beside the title exports the
 * members for whoever reads them (8.41, 8.11).
 */
export function MembersPage() {
  const t = useTranslations();
  const roleName = useRoleName();
  const can = useCan();
  const failureText = useFailureText();
  const { tenant, detail } = useTenant();
  const [params, setParams] = useListParams();
  const [inviting, setInviting] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [announcement, setAnnouncement] = useState<string | null>(null);
  // The command menu's "Invite member" opens the page with `?invite=1`: the dialog opens once, and the address forgets it, as the
  // dialog's state is the page's own (F8).
  const [invite, setInvite] = useQueryState("invite", { history: "replace" });
  useEffect(() => {
    if (invite !== "1") return;
    setInviting(true);
    void setInvite(null);
  }, [invite, setInvite]);
  const me = useQuery(getGetMeQueryOptions());
  const plan = usePlan();
  const planReason = usePlanReason();
  const bannerId = useId();
  const onSearch = useCallback(
    (next: string) => void setParams({ q: next || null }),
    [setParams],
  );
  const seesInvitations = can(Permission.TenantInvitationsRead);
  const readsMembers = can(Permission.TenantMembersRead);
  // The role filter's choices are the tenant's roles, for whom may read them.
  const readsRoles = can(Permission.TenantRolesRead);
  const roles = useTenantRoles();
  // A person without the Invitations tab who opens ?tab=invitations sees the Members tab.
  const tab: Tab = seesInvitations ? params.tab : "members";
  const { q, roleId, sort } = params;
  const search = { q: q || undefined, roleId: roleId ?? undefined };

  const memberSort = Object.values(ListMembersSort).find(
    (value) => value === sort,
  );
  const members = useInfiniteQuery({
    ...memberPagesOptions(
      tenant.id,
      { ...search, sort: memberSort },
      readsMembers && tab === "members",
    ),
    placeholderData: keepPreviousData,
  });
  const invitations = useInfiniteQuery({
    ...invitationPagesOptions(
      tenant.id,
      {
        ...search,
        sort: Object.values(ListInvitationsSort).find(
          (value) => value === sort,
        ),
      },
      seesInvitations && tab === "invitations",
    ),
    placeholderData: keepPreviousData,
  });
  // The tabs' counts are the totals without a filter, so a search does not change them (8.06). The one item of the members' count
  // is the settings menu's read (F59).
  const memberCount = useQuery(
    getListMembersQueryOptions(
      tenant.id,
      { pageSize: 1 },
      { query: { enabled: readsMembers } },
    ),
  );
  const invitationCount = useQuery(
    getListInvitationsQueryOptions(
      tenant.id,
      { pageSize: 1 },
      { query: { enabled: seesInvitations } },
    ),
  );
  const list = tab === "members" ? members : invitations;

  if (!readsMembers)
    return (
      <Frame>
        <PermissionDenied page={t("settings.members")} />
      </Frame>
    );

  const canInvite = can(Permission.TenantInvitationsCreate);
  const memberLimit = plan.limit(LimitKey.MembersMax);
  const membersTotal = memberCount.data?.total;
  const pending =
    membersTotal === undefined
      ? 0
      : plan.seats(LimitKey.MembersMax, membersTotal).invitations;
  const viewInvitations = () =>
    void setParams({ tab: "invitations", q: null, roleId: null, sort: null });

  const inviteButton = (variant: "primary" | "secondary") =>
    canInvite && (
      <Button
        variant={variant}
        icon={variant === "primary" ? <Icon icon={PlusIcon} /> : undefined}
        onClick={() => setInviting(true)}
        className={variant === "primary" ? "max-tablet:w-full" : undefined}
      >
        {t("members.invite")}
      </Button>
    );

  const filtered = Boolean(q || roleId);
  const rowCount = rowsIn(list.data);
  // The empty state of a filtered list offers its own clear button.
  const emptyFiltered = filtered && rowCount === 0;
  const clear = () => void setParams({ q: null, roleId: null });
  const timeZone = me.data?.timeZone;

  // The next page: a 401 goes to sign-in (F4), a 403 of a stopped organisation is the tenant layout's (F47), and any other
  // failure says so and keeps the rows that came.
  const loadMore = async () => {
    const before = rowCount ?? 0;
    const result = await list.fetchNextPage();
    if (!result.isFetchNextPageError) {
      const after = rowsIn(result.data) ?? 0;
      setAnnouncement(
        t(
          tab === "members"
            ? "members.loadedMembers"
            : "members.loadedInvitations",
          { count: after - before },
        ),
      );
      return;
    }
    const action =
      result.error instanceof ProblemDetailsError
        ? actionFor(result.error.status, result.error.problem).kind
        : null;
    if (action === "sign-in")
      return window.location.assign(signInAgainUrl(window.location));
    if (action === "tenant-blocked") return;
    showToast({
      title: t(
        tab === "members"
          ? "members.moreMembersError"
          : "members.moreInvitationsError",
      ),
      description: failureText(problemOf(result.error)),
      tone: "destructive",
    });
  };

  const body = (() => {
    if (list.isPending)
      return (
        <TableSkeleton
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
    if (rowCount === 0 && filtered)
      return (
        <EmptyState
          kind="no-results"
          icon={SearchXIcon}
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
    if (rowCount === 0 && tab === "invitations")
      return (
        <EmptyState
          icon={UserPlusIcon}
          title={t("members.noInvitations")}
          description={t("members.noInvitationsText")}
          action={inviteButton("secondary")}
        />
      );
    const table = {
      sort,
      timeZone,
      onSortChange: (next: string | null) =>
        void setParams({ sort: sortValue(next) }),
    };
    const more = (
      <LoadMore
        hasMore={list.hasNextPage}
        loading={list.isFetchingNextPage}
        onMore={() => void loadMore()}
        label={t("members.loadMore")}
        loadingLabel={
          tab === "members"
            ? t("members.loadingMoreMembers")
            : t("members.loadingMoreInvitations")
        }
        announcement={announcement}
      />
    );
    if (tab === "members" && members.data) {
      const rows = members.data.pages.flatMap((page) => page.items);
      // Only the viewer, with no filter (8.04): the table, then what to do next.
      const onlyYou = !filtered && members.data.pages[0]?.total === 1;
      return (
        <div className="flex flex-col gap-4">
          <div className="flex flex-col">
            <MembersTable rows={rows} meId={me.data?.id} {...table} />
            {more}
          </div>
          {onlyYou && (
            <EmptyState
              icon={UserPlusIcon}
              title={t("members.onlyYou")}
              description={t("members.onlyYouText")}
              action={inviteButton("secondary")}
            />
          )}
        </div>
      );
    }
    if (!invitations.data) return null;
    const rows = invitations.data.pages.flatMap((page) => page.items);
    return (
      <div className="flex flex-col">
        <InvitationsTable rows={rows} {...table} />
        {more}
      </div>
    );
  })();

  // The banner's text (8.09, 8.43): at the limit the limit's reason; over it the seats and what still works.
  const banner = (() => {
    if (!canInvite || !memberLimit.full || memberLimit.limit === null)
      return null;
    if (memberLimit.atLimit) return planReason.limit(LimitKey.MembersMax);
    return {
      title: t("plans.membersOver", {
        count: memberLimit.count,
        limit: memberLimit.limit,
      }),
      text:
        membersTotal === undefined
          ? null
          : t("plans.membersOverText", {
              ...plan.seats(LimitKey.MembersMax, membersTotal),
              limit: memberLimit.limit,
            }),
    };
  })();
  // Over the limit, the banner offers the invitations while one is pending, for whom may read them (F56).
  const bannerAction =
    memberLimit.overLimit && seesInvitations && pending > 0 ? (
      <Button onClick={viewInvitations}>{t("plans.viewInvitations")}</Button>
    ) : null;
  // No counts while the list loads or cannot load (8.02, 8.03).
  const counted = list.isSuccess;
  // Export follows the Members tab's filter and sort; from the Invitations tab it is every member (8.08, 8.10). Its count is the
  // list's total for that filter, and with nobody in it the item says why (8.14, F51).
  const onMembers = tab === "members";
  const exportCount = onMembers ? members.data?.pages[0]?.total : membersTotal;
  const exportFilter = {
    q: onMembers ? search.q : undefined,
    roleId: onMembers ? search.roleId : undefined,
    sort: onMembers ? memberSort : undefined,
    roleName: (() => {
      const role = onMembers
        ? roles.data?.items.find((choice) => choice.id === roleId)
        : undefined;
      return role && roleName(role);
    })(),
  };
  const noMatch = exportFilter.q
    ? t("members.exportNoMatchSearch")
    : t("members.exportNoMatchFilter");

  return (
    <Frame>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex min-w-0 flex-col gap-0.5">
          <div className="flex items-center gap-1">
            <h1 className="text-page-title">{t("members.title")}</h1>
            <RowMenu
              label={t("members.moreActions")}
              items={[
                {
                  label: t("members.exportCsv"),
                  onSelect: () => setExporting(true),
                  // Still counting: the item waits, disabled.
                  disabled: !exportCount,
                  reason: exportCount === 0 ? noMatch : undefined,
                },
              ]}
            />
          </div>
          <p className="text-body text-muted-foreground">
            {t("members.description", { organisation: detail.name })}
          </p>
        </div>
        {inviteButton("primary")}
      </div>
      {banner && (
        <PlanBanner
          id={bannerId}
          title={banner.title}
          text={banner.text ?? ""}
          action={bannerAction}
        />
      )}
      {seesInvitations && (
        <Tabs
          label={t("members.tabs")}
          current={tab}
          items={[
            {
              id: "members",
              label: t("members.tabMembers"),
              count: counted ? membersTotal : undefined,
            },
            {
              id: "invitations",
              label: t("members.tabInvitations"),
              count: counted ? invitationCount.data?.total : undefined,
            },
          ]}
          onValueChange={(next) =>
            void setParams({
              tab: next === "invitations" ? "invitations" : null,
              q: null,
              roleId: null,
              sort: null,
            })
          }
        />
      )}
      <div className="flex flex-wrap items-center gap-2">
        <div className="min-w-0 flex-1 tablet:flex-none">
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
        {readsRoles && (
          <Combobox
            variant="filter"
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
              })
            }
            searchPlaceholder={t("members.roleSearch")}
            emptyLabel={t("members.roleEmpty")}
          />
        )}
        {roleId && !emptyFiltered && (
          <Button
            variant="ghost"
            onClick={() => void setParams({ roleId: null })}
          >
            {t("members.clearFilter")}
          </Button>
        )}
      </div>
      {body}
      {exporting && exportCount !== undefined && (
        <ExportMembersDialog
          count={exportCount}
          filter={exportFilter}
          onClose={() => setExporting(false)}
        />
      )}
      {inviting &&
        (memberLimit.full ? (
          <NoFreeSeatDialog
            members={membersTotal}
            onViewInvitations={
              seesInvitations
                ? () => {
                    setInviting(false);
                    viewInvitations();
                  }
                : undefined
            }
            onClose={() => setInviting(false)}
          />
        ) : (
          <InviteDialog onClose={() => setInviting(false)} />
        ))}
    </Frame>
  );
}

function Frame({ children }: { children: ReactNode }) {
  return <section className="flex min-w-0 flex-col gap-4">{children}</section>;
}
