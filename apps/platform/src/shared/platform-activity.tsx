"use client";

import {
  actionFor,
  ErrorCode,
  getGetMeQueryKey,
  getGetMeQueryOptions,
  getListPlatformAuditEntriesInfiniteQueryOptions,
  ProblemDetailsError,
  problemOf,
} from "@repo/api-client";
import { ActivityLog } from "@repo/ui/components/activity-log";
import { Banner } from "@repo/ui/components/banner";
import { EmptyState } from "@repo/ui/components/empty-state";
import { UserPlusIcon } from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { LoadMore } from "@repo/ui/components/load-more";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { Skeleton } from "@repo/ui/components/skeleton";
import { pageSize, rowsIn } from "@repo/ui/lib/pages";
import {
  useInfiniteQuery,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { ActivitySentence } from "./activity-sentence";
import { leaveOf } from "./problem-screen";

/**
 * The platform's log, newest first (`ListPlatformAuditEntries`, K5's cursor contract). With a tenant id it holds the platform's
 * own entries about that organisation; it never reads the organisation's own log, which would be support access.
 */
export function usePlatformActivity(tenantId?: string) {
  const options = getListPlatformAuditEntriesInfiniteQueryOptions(
    { limit: pageSize, ...(tenantId ? { tenantId } : {}) },
    {
      query: {
        initialPageParam: undefined,
        getNextPageParam: (last) => last.nextCursor ?? undefined,
      },
    },
  );
  return { log: useInfiniteQuery(options), queryKey: options.queryKey };
}

/**
 * The log grouped by day in the system admin's zone, with its states: loading (7.03), empty (7.05), the error with its reference
 * and Try again (7.04). "Load older activity" reads the next page by its cursor until `nextCursor` is null, with the Loading more
 * row and the live region of F9 (7.02); a failed page says so under the entries with Try again (7.06), and a cursor the API no
 * longer reads starts again from the newest entry with a Banner above the entries (7.07).
 */
export function PlatformActivity({
  activity,
  emptyText,
}: {
  activity: ReturnType<typeof usePlatformActivity>;
  emptyText: string;
}) {
  const t = useTranslations();
  const queryClient = useQueryClient();
  const me = useQuery(getGetMeQueryOptions());
  const { log, queryKey } = activity;
  const [announcement, setAnnouncement] = useState<string | null>(null);
  const [olderFailed, setOlderFailed] = useState(false);
  const [restarted, setRestarted] = useState(false);

  // A cursor the API no longer reads (paging.cursor_invalid) starts the log again from the newest entry (7.07).
  const loadOlder = async () => {
    const before = rowsIn(log.data) ?? 0;
    setOlderFailed(false);
    setRestarted(false);
    const result = await log.fetchNextPage();
    if (!result.isFetchNextPageError) {
      setAnnouncement(
        t("activity.loadedOlder", {
          count: (rowsIn(result.data) ?? 0) - before,
        }),
      );
      return;
    }
    // A 401 goes to sign-in (F4). A refusal of the caller's account is final: GetMe's answer decides what the shell shows (F47).
    const action =
      result.error instanceof ProblemDetailsError
        ? actionFor(result.error.status, result.error.problem).kind
        : null;
    if (action === "sign-in")
      return window.location.assign(signInAgainUrl(window.location));
    if (action === "account-suspended" || action === "account-deleted") {
      await queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
      return;
    }
    if (problemOf(result.error)?.code === ErrorCode.PagingCursorInvalid) {
      setRestarted(true);
      await queryClient.resetQueries({ queryKey });
      return;
    }
    setOlderFailed(true);
  };

  // The days follow the system admin's zone, so the log waits for GetMe.
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
        icon={UserPlusIcon}
        title={t("activity.empty")}
        description={emptyText}
      />
    );
  return (
    <div className="flex flex-col gap-2">
      {/* The Banner's title only: new entries never break a cursor, so the frame's second line would not be true (7.07). */}
      {restarted && <Banner title={t("activity.restarted")} />}
      <ActivityLog
        entries={entries}
        sentence={(entry) => <ActivitySentence entry={entry} />}
        locale={t("locale.format")}
        timeZone={me.data?.timeZone}
        labels={{
          today: (date) => t("activity.today", { date }),
          yesterday: (date) => t("activity.yesterday", { date }),
          older: t("activity.older"),
        }}
        // LoadMore draws the foot of the log (F9), so the component's own button stays away, as in the web.
        hasOlder={false}
        loadingOlder={false}
        onOlder={() => {}}
      />
      <LoadMore
        hasMore={log.hasNextPage}
        loading={log.isFetchingNextPage}
        onMore={() => void loadOlder()}
        label={t("activity.older")}
        loadingLabel={t("activity.loadingOlder")}
        announcement={announcement}
        failed={
          olderFailed
            ? {
                text: t("activity.olderError"),
                retryLabel: t("common.tryAgain"),
              }
            : undefined
        }
      />
    </div>
  );
}
