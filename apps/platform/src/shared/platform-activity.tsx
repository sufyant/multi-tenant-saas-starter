"use client";

import {
  actionFor,
  ErrorCode,
  getGetMeQueryOptions,
  getListPlatformAuditEntriesInfiniteQueryOptions,
  ProblemDetailsError,
} from "@repo/api-client";
import { ActivityLog } from "@repo/ui/components/activity-log";
import { EmptyState } from "@repo/ui/components/empty-state";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import {
  useInfiniteQuery,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { ActivitySentence } from "./activity-sentence";
import { ListError } from "./list-error";
import { problemOf } from "./problem";
import { useFailureText } from "./use-failure-text";

/** The entries of one page of the log. The API's limit is 1 to 100. */
const pageSize = 50;

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
 * The log grouped by day in the system admin's zone, with its states (the ListStates board): loading, empty, the error with its
 * reference and Try again. "Load older activity" reads the next page by its cursor until `nextCursor` is null.
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
  const failureText = useFailureText();
  const me = useQuery(getGetMeQueryOptions());
  const { log, queryKey } = activity;

  // A cursor the API no longer reads (paging.cursor_invalid) starts the log again from the newest entry (the Messages board).
  const loadOlder = async () => {
    const result = await log.fetchNextPage();
    if (!result.isFetchNextPageError) return;
    // A 401 goes to sign-in (F4).
    if (
      result.error instanceof ProblemDetailsError &&
      actionFor(result.error.status, result.error.problem).kind === "sign-in"
    )
      return window.location.assign("/sign-in");
    if (problemOf(result.error)?.code === ErrorCode.PagingCursorInvalid) {
      showToast({ title: t("activity.restarted"), tone: "info" });
      await queryClient.resetQueries({ queryKey });
      return;
    }
    showToast({
      title: t("activity.olderError"),
      description: failureText(result.error),
      tone: "destructive",
    });
  };

  // The days follow the system admin's zone, so the log waits for GetMe.
  if (log.isPending || me.isPending)
    return <Skeleton rows={4} label={t("activity.loading")} />;
  if (!log.data)
    return (
      <ListError
        title={t("activity.error")}
        error={log.error}
        at={log.errorUpdatedAt}
        onRetry={() => void log.refetch()}
      />
    );
  const entries = log.data.pages.flatMap((page) => page.items);
  if (entries.length === 0)
    return <EmptyState title={t("activity.empty")} description={emptyText} />;
  return (
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
      hasOlder={log.hasNextPage}
      loadingOlder={log.isFetchingNextPage}
      onOlder={() => void loadOlder()}
    />
  );
}
