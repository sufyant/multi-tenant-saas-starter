"use client";

import {
  actionFor,
  type Bell,
  type CursorPageOfNotificationResponse,
  getGetMeQueryKey,
  getGetMeQueryOptions,
  markNotificationRead,
  type NotificationResponse,
  ProblemDetailsError,
  platformBell,
  problemOf,
} from "@repo/api-client";
import {
  NotificationBell,
  type NotificationBellProps,
  type NotificationEntry,
  type NotificationListState,
  NotificationPage,
  type NotificationPageProps,
  type NotificationTab,
} from "@repo/ui/components/notification-bell";

type BellPlace = NotificationBellProps["place"];

import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import {
  type InfiniteData,
  type UseInfiniteQueryResult,
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { notificationHref } from "./notification-link";

/** The platform's bell (`GET /v1/platform/notifications`). On a phone it opens the page of notifications (3.08). */
export function PlatformBell({
  place,
}: {
  /** Where the shell draws it: a sidebar row, a rail icon or the phone's top bar. */
  place?: BellPlace;
}) {
  const [open, setOpen] = useState(false);
  const { bell, setTab } = useBell(platformBell(), open, () => setOpen(false));
  return (
    <NotificationBell
      {...bell}
      open={open}
      onOpenChange={(next) => {
        if (next) setTab(bell.unread ? "unread" : "all");
        setOpen(next);
      }}
      place={place}
      pageHref="/notifications"
    />
  );
}

/** The page of notifications, which the bell opens on a phone (3.08): on Unread while something is unread, else on All. */
export function PlatformNotifications() {
  const { bell } = useBell(platformBell(), true, () => {});
  return <NotificationPage {...bell} />;
}

/**
 * A bell's reads and commands around the shared `NotificationBell`. The count reads at once; the list reads while the bell is
 * open, on Unread while something is unread and on All otherwise (3.01, 3.03); each tab reads its own list. Opening an item
 * marks it read and follows its link at once, without waiting for the answer. Mark all as read waits for the answer, then reads
 * the bell again and shows All, or says why it failed (F47). The live stream keeps both current (`notification-stream.tsx`).
 */
function useBell(
  bell: Bell,
  open: boolean,
  close: () => void,
): { bell: NotificationPageProps; setTab: (tab: NotificationTab) => void } {
  const router = useRouter();
  const queryClient = useQueryClient();
  const t = useTranslations();
  const failureText = useFailureText();
  // Until the reader or the bell chooses, the tab follows the count.
  const [chosen, setTab] = useState<NotificationTab>();
  // A page that failed on GetMe may hold the bell ("No access yet"): reading it again on mount would take that page away.
  const me = useQuery({ ...getGetMeQueryOptions(), retryOnMount: false });
  const count = useQuery(bell.count);
  const tab = chosen ?? (count.data?.unread ? "unread" : "all");
  const list = useInfiniteQuery({
    ...(tab === "unread" ? bell.unreadList : bell.list),
    enabled: open && (chosen !== undefined || !count.isPending),
    staleTime: 0,
  });
  const readAgain = () =>
    Promise.all(
      bell.keys.map((queryKey) => queryClient.invalidateQueries({ queryKey })),
    );
  const markAll = useMutation({
    mutationFn: bell.markAll,
    // Nothing is unread now: the bell shows All (3.01 leads to 3.09).
    onSuccess: () => {
      setTab("all");
      return readAgain();
    },
    onError: (error) => {
      const action =
        error instanceof ProblemDetailsError
          ? actionFor(error.status, error.problem)
          : null;
      // A 401 goes to sign-in; a suspended or deleted user reads GetMe again, and the shell shows its screen (F4, F47).
      if (action?.kind === "sign-in")
        return window.location.assign(signInAgainUrl(window.location));
      if (
        action?.kind === "account-suspended" ||
        action?.kind === "account-deleted"
      )
        return void queryClient.invalidateQueries({
          queryKey: getGetMeQueryKey(),
        });
      showToast({
        title: t("notifications.markAllFailed"),
        description: failureText(problemOf(error)),
        tone: "destructive",
      });
    },
  });
  const markRead = useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSettled: readAgain,
  });

  const entryOf = (item: NotificationResponse) => ({
    ...item,
    href: notificationHref(item.link),
    area: item.area,
    actor: item.actorName ?? undefined,
  });

  return {
    bell: {
      unread: count.data?.unread,
      list: stateOf(list, entryOf),
      tab,
      onTabChange: setTab,
      onOpenItem: (entry, { follow }) => {
        markRead.mutate(entry.id);
        if (!follow) return;
        close();
        router.push(entry.href);
      },
      onMarkAll: () => markAll.mutate(),
      markingAll: markAll.isPending,
      timeZone: me.data?.timeZone,
      linkComponent: Link,
    },
    setTab,
  };
}

/** The list's read as the bell shows it: its pages with each item's address and organisation, its failure, or the wait. */
function stateOf(
  list: UseInfiniteQueryResult<
    InfiniteData<CursorPageOfNotificationResponse>,
    Error
  >,
  entryOf: (item: NotificationResponse) => NotificationEntry,
): NotificationListState {
  if (list.data)
    return {
      status: "success",
      items: list.data.pages.flatMap((page) => page.items.map(entryOf)),
      hasMore: list.hasNextPage,
      loadingMore: list.isFetchingNextPage,
      onLoadMore: () => void list.fetchNextPage(),
    };
  if (list.error)
    return {
      status: "error",
      problem: problemOf(list.error),
      at: list.errorUpdatedAt,
      onRetry: () => void list.refetch(),
    };
  return { status: "loading" };
}
