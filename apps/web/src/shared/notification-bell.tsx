"use client";

import {
  type Bell,
  type CursorPageOfNotificationResponse,
  getGetMeQueryOptions,
  getListMyTenantsQueryOptions,
  markNotificationRead,
  type NotificationResponse,
  personalBell,
  problemOf,
  schemas,
  tenantBell,
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

/** The bell inside an organisation: that tenant's notifications and the personal ones (`GET /v1/tenants/{tenantId}/notifications`). */
export function TenantBell({ tenantId }: { tenantId: string }) {
  return <BellOf bell={tenantBell(tenantId)} />;
}

/** The bell of "No access yet" and Choose an organisation: the personal notifications only (`GET /v1/me/notifications`). */
export function PersonalBell() {
  return <BellOf bell={personalBell()} />;
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
  // The cached list of the tenant layout gives each link its organisation's slug (F30).
  const tenants = useQuery(
    getListMyTenantsQueryOptions({
      pageSize: schemas.listMyTenantsQueryPageSizeMax,
    }),
  );
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

  const hrefOf = (item: NotificationResponse) =>
    notificationHref(item.link, tenants.data?.items ?? []);
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
