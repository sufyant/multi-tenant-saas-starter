"use client";

import {
  actionFor,
  ErrorCode,
  getGetAuditRetentionQueryOptions,
  getGetMeQueryOptions,
  getListAuditEntriesInfiniteQueryOptions,
  Permission,
  ProblemDetailsError,
  problemOf,
} from "@repo/api-client";
import { ActivityLog } from "@repo/ui/components/activity-log";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { EmptyState } from "@repo/ui/components/empty-state";
import { UserPlusIcon } from "@repo/ui/components/icon";
import { ListError } from "@repo/ui/components/list-error";
import { LoadMore } from "@repo/ui/components/load-more";
import { RowMenu } from "@repo/ui/components/row-menu";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { showToast } from "@repo/ui/components/toast";
import { formatDate } from "@repo/ui/lib/format-date-time";
import { pageSize, rowsIn } from "@repo/ui/lib/pages";
import {
  useInfiniteQuery,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { type ReactNode, useState } from "react";
import { PermissionDenied } from "@/shared/permission-denied";
import { leaveOf } from "@/shared/problem-screen";
import { useCan, useTenant } from "@/shared/tenant-context";
import { ActivitySentence } from "./activity-sentence";
import { ActivitySkeleton } from "./activity-skeleton";
import { ExportActivityDialog } from "./export-activity-dialog";

/**
 * Settings > Activity log (12.01 to 12.08): the tenant log, newest first, grouped by day in the viewer's zone, under the frame's
 * description and no Freshness (F59). "Show older activity" reads the next page by its cursor (K5) until `nextCursor` is null,
 * with the Loading more row and the live region of F9; a failed page says so under the entries with Try again (12.06). The More
 * menu beside the title exports the log as a CSV file (12.08). Only who may read the log sees it (F5). Under the description the
 * log says its period, and while a shorter period holds older entries a Banner says when they go, with Export (12.10, 12.11);
 * the export's permission is the log's own, so every viewer of the page may export. Both come from GetAuditRetention, and the
 * screen computes no period and no date.
 */
export function ActivityPage() {
  const t = useTranslations();
  const can = useCan();
  const { tenant } = useTenant();
  const queryClient = useQueryClient();
  const me = useQuery(getGetMeQueryOptions());
  const [announcement, setAnnouncement] = useState<string | null>(null);
  const [olderFailed, setOlderFailed] = useState(false);
  const [exporting, setExporting] = useState(false);
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
  const retention = useQuery(
    getGetAuditRetentionQueryOptions(tenant.id, {
      query: { enabled: readsLog },
    }),
  );

  if (!readsLog)
    return (
      <Frame>
        <PermissionDenied page={t("shell.activityLog")} />
      </Frame>
    );

  const timeZone = me.data?.timeZone;
  const locale = t("locale.format");
  const shortening = retention.data?.shortening;

  // A cursor the API no longer reads (paging.cursor_invalid) starts the log again from the newest entry (the Messages board).
  const showOlder = async () => {
    const before = rowsIn(log.data) ?? 0;
    setOlderFailed(false);
    const result = await log.fetchNextPage();
    if (!result.isFetchNextPageError) {
      setAnnouncement(
        t("activity.loadedOlder", {
          count: (rowsIn(result.data) ?? 0) - before,
        }),
      );
      return;
    }
    // A 401 goes to sign-in (F4); a 403 of a stopped organisation is the tenant layout's (F47).
    const action =
      result.error instanceof ProblemDetailsError
        ? actionFor(result.error.status, result.error.problem).kind
        : null;
    if (action === "sign-in")
      return window.location.assign(signInAgainUrl(window.location));
    if (action === "tenant-blocked") return;
    if (problemOf(result.error)?.code === ErrorCode.PagingCursorInvalid) {
      showToast({ title: t("activity.restarted"), tone: "info" });
      await queryClient.resetQueries({ queryKey: options.queryKey });
      return;
    }
    setOlderFailed(true);
  };

  const body = (() => {
    // The days follow the viewer's zone, so the log waits for GetMe.
    if (log.isPending || me.isPending)
      return <ActivitySkeleton label={t("activity.loading")} />;
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
          description={t("activity.emptyText")}
        />
      );
    return (
      <div className="flex flex-col gap-2">
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
          // The web's LoadMore draws the foot of the log (F9), so the component's own button stays away.
          hasOlder={false}
          loadingOlder={false}
          onOlder={() => {}}
        />
        <LoadMore
          hasMore={log.hasNextPage}
          loading={log.isFetchingNextPage}
          onMore={() => void showOlder()}
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
  })();

  return (
    <Frame>
      <div className="flex min-w-0 flex-col gap-0.5">
        <div className="flex items-center gap-1">
          <h1 className="text-page-title">{t("activity.log")}</h1>
          <RowMenu
            label={t("activity.moreActions")}
            align="start"
            items={[
              {
                label: t("activity.exportCsv"),
                onSelect: () => setExporting(true),
              },
            ]}
          />
        </div>
        <p className="text-body text-muted-foreground">
          {t("activity.description")}
        </p>
        {retention.data && (
          <p className="text-body text-muted-foreground">
            {/* The days, or no time limit for null (12.01). */}
            {retention.data.days === null
              ? t("activity.retention.keptNoLimit")
              : t("activity.retention.kept", { days: retention.data.days })}
          </p>
        )}
      </div>
      {/* The date waits for GetMe, so it names the day in the viewer's zone (F18). */}
      {shortening && !me.isPending && (
        <Banner
          tone="warning"
          title={t("activity.retention.goesTitle", {
            days: shortening.days,
            date: formatDate(shortening.goesAt, { locale, timeZone }),
          })}
          action={
            <Button onClick={() => setExporting(true)}>
              {t("activity.exportCsv")}
            </Button>
          }
        >
          {t("activity.retention.goesText", { days: shortening.days })}
        </Banner>
      )}
      {body}
      {exporting && (
        <ExportActivityDialog onClose={() => setExporting(false)} />
      )}
    </Frame>
  );
}

function Frame({ children }: { children: ReactNode }) {
  return <section className="flex min-w-0 flex-col gap-4">{children}</section>;
}
