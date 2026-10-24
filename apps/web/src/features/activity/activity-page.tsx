"use client";

import {
  actionFor,
  ErrorCode,
  getGetMeQueryOptions,
  getListAuditEntriesInfiniteQueryOptions,
  Permission,
  ProblemDetailsError,
  problemOf,
} from "@repo/api-client";
import { ActivityLog } from "@repo/ui/components/activity-log";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Freshness } from "@repo/ui/components/freshness";
import { ListError } from "@repo/ui/components/list-error";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import {
  useInfiniteQuery,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { PermissionDenied } from "@/shared/permission-denied";
import { leaveOf } from "@/shared/problem-screen";
import { SettingsNav } from "@/shared/settings-nav";
import { useCan, useTenant } from "@/shared/tenant-context";
import { ActivitySentence } from "./activity-sentence";

/** The entries of one page of the log. The API's limit is 1 to 100. */
const pageSize = 50;

/**
 * Settings > Activity (the Activity board): the tenant log, newest first, grouped by day in the viewer's zone. "Show older
 * activity" reads the next page by its cursor (K5) until `nextCursor` is null. Only who may read the log sees it (F5).
 */
export function ActivityPage() {
  const t = useTranslations();
  const can = useCan();
  const { tenant } = useTenant();
  const queryClient = useQueryClient();
  const failureText = useFailureText();
  const me = useQuery(getGetMeQueryOptions());
  const readsLog = can(Permission.TenantAuditRead);
  const options = getListAuditEntriesInfiniteQueryOptions(
    tenant.id,
    { limit: pageSize },
    {
      query: {
        enabled: readsLog,
        initialPageParam: undefined,
        getNextPageParam: (last) => last.nextCursor ?? undefined,
      },
    },
  );
  const log = useInfiniteQuery(options);

  if (!readsLog)
    return (
      <Frame>
        <PermissionDenied page={t("settings.activity")} />
      </Frame>
    );

  const timeZone = me.data?.timeZone;
  const locale = t("locale.format");

  // A cursor the API no longer reads (paging.cursor_invalid) starts the log again from the newest entry (the Messages board).
  const showOlder = async () => {
    const result = await log.fetchNextPage();
    if (!result.isFetchNextPageError) return;
    // A 401 goes to sign-in (F4); a 403 of a stopped organisation is the tenant layout's (F47).
    const action =
      result.error instanceof ProblemDetailsError
        ? actionFor(result.error.status, result.error.problem).kind
        : null;
    if (action === "sign-in") return window.location.assign("/sign-in");
    if (action === "tenant-blocked") return;
    if (problemOf(result.error)?.code === ErrorCode.PagingCursorInvalid) {
      showToast({ title: t("activity.restarted"), tone: "info" });
      await queryClient.resetQueries({ queryKey: options.queryKey });
      return;
    }
    showToast({
      title: t("activity.olderError"),
      description: failureText(problemOf(result.error)),
      tone: "destructive",
    });
  };

  const body = (() => {
    // The days follow the viewer's zone, so the log waits for GetMe.
    if (log.isPending || me.isPending)
      return <Skeleton rows={4} label={t("activity.loading")} />;
    if (!log.data)
      return (
        <ListError
          title={t("activity.error")}
          problem={problemOf(log.error)}
          leave={leaveOf(log.error)}
          at={log.errorUpdatedAt}
          onRetry={() => void log.refetch()}
        />
      );
    const entries = log.data.pages.flatMap((page) => page.items);
    if (entries.length === 0)
      return (
        <EmptyState
          title={t("activity.empty")}
          description={t("activity.emptyText")}
        />
      );
    return (
      <ActivityLog
        entries={entries}
        sentence={(entry) => (
          <ActivitySentence entry={entry} timeZone={timeZone} />
        )}
        locale={locale}
        timeZone={timeZone}
        labels={{
          today: (date) => t("activity.today", { date }),
          yesterday: (date) => t("activity.yesterday", { date }),
          older: t("activity.older"),
        }}
        hasOlder={log.hasNextPage}
        loadingOlder={log.isFetchingNextPage}
        onOlder={() => void showOlder()}
      />
    );
  })();

  return (
    <Frame>
      <div>
        <h1 className="text-page-title">{t("activity.title")}</h1>
        {log.dataUpdatedAt > 0 && (
          <Freshness
            at={new Date(log.dataUpdatedAt)}
            locale={locale}
            timeZone={timeZone}
            label={t("common.updated")}
          />
        )}
      </div>
      {body}
    </Frame>
  );
}

function Frame({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="activity" />
      <section className="flex min-w-0 flex-1 flex-col gap-4">
        {children}
      </section>
    </div>
  );
}
