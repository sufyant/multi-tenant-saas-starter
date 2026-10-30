"use client";

import {
  type Bell,
  type CursorPageOfNotificationResponse,
  getGetMeQueryOptions,
  markNotificationRead,
  type NotificationResponse,
  platformBell,
  problemOf,
} from "@repo/api-client";
import {
  NotificationBell,
  type NotificationListState,
} from "@repo/ui/components/notification-bell";
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
import { useState } from "react";
import { notificationHref } from "./notification-link";

/** The platform's bell (`GET /v1/platform/notifications`). */
export function PlatformBell() {
  return <BellOf bell={platformBell()} />;
}

/**
 * A bell's reads and commands around the shared `NotificationBell`. The count reads at once; the list reads each time the bell
 * opens. Opening an item marks it read and follows its link at once, without waiting for the answer. Mark all as read waits for
 * the answer, then reads the bell again (F47). The live stream keeps both current (`notification-stream.tsx`).
 */
function BellOf({ bell }: { bell: Bell }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  // A page that failed on GetMe may hold the bell ("No access yet"): reading it again on mount would take that page away.
  const me = useQuery({ ...getGetMeQueryOptions(), retryOnMount: false });
  const count = useQuery(bell.count);
  const list = useInfiniteQuery({ ...bell.list, enabled: open, staleTime: 0 });
  const readAgain = () =>
    Promise.all(
      bell.keys.map((queryKey) => queryClient.invalidateQueries({ queryKey })),
    );
  const markAll = useMutation({
    mutationFn: bell.markAll,
    onSuccess: readAgain,
  });
  const markRead = useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSettled: readAgain,
  });

  const hrefOf = (item: NotificationResponse) => notificationHref(item.link);
  const state = stateOf(list, hrefOf);

  return (
    <NotificationBell
      unread={count.data?.unread}
      open={open}
      onOpenChange={setOpen}
      list={state}
      onOpenItem={(entry) => {
        markRead.mutate(entry.id);
        setOpen(false);
        router.push(entry.href);
      }}
      onMarkAll={() => markAll.mutate()}
      markingAll={markAll.isPending}
      timeZone={me.data?.timeZone}
      linkComponent={Link}
    />
  );
}

/** The list's read as the bell shows it: its pages with each item's address, its failure, or the wait. */
function stateOf(
  list: UseInfiniteQueryResult<
    InfiniteData<CursorPageOfNotificationResponse>,
    Error
  >,
  hrefOf: (item: NotificationResponse) => string,
): NotificationListState {
  if (list.data)
    return {
      status: "success",
      items: list.data.pages.flatMap((page) =>
        page.items.map((item) => ({ ...item, href: hrefOf(item) })),
      ),
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
