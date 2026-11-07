"use client";

import {
  actionFor,
  type Bell,
  type CursorPageOfNotificationResponse,
  getGetMeQueryKey,
  getGetMeQueryOptions,
  getListMyTenantsQueryOptions,
  markNotificationRead,
  type NotificationResponse,
  ProblemDetailsError,
  personalBell,
  problemOf,
  schemas,
  tenantBell,
} from "@repo/api-client";
import {
  NotificationBell,
  type NotificationBellProps,
  type NotificationEntry,
  type NotificationListState,
  type NotificationTab,
} from "@repo/ui/components/notification-bell";

type BellPlace = NotificationBellProps["place"];

import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useOffline } from "@repo/ui/lib/use-online";
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
import { useServerReach } from "./server-bar";

/** The bell inside an organisation: that tenant's notifications and the personal ones (`GET /v1/tenants/{tenantId}/notifications`). */
export function TenantBell({
  tenantId,
  slug,
  place,
}: {
  tenantId: string;
  /** The organisation's slug: a link that would leave the shell stays in this organisation (`notificationHref`). */
  slug: string;
  /** Where the shell draws it: a sidebar row, a rail icon or the phone's top bar. */
  place?: BellPlace;
}) {
  return <BellOf bell={tenantBell(tenantId)} current={slug} place={place} />;
}

/**
 * The bell of "No access yet", Choose an organisation and the pages without an organisation: the personal notifications only
 * (`GET /v1/me/notifications`).
 */
export function PersonalBell({ place }: { place?: BellPlace }) {
  return <BellOf bell={personalBell()} place={place} />;
}

/**
 * A bell's reads and commands around the shared `NotificationBell`. The count reads at once; the list reads each time the bell
 * opens, on Unread while something is unread and on All otherwise (3.01, 3.03); each tab reads its own list. Opening an item
 * marks it read and follows its link at once, without waiting for the answer. Mark all as read waits for the answer, then reads
 * the bell again and shows All, or says why it failed (F47). The live stream keeps both current (`notification-stream.tsx`).
 */
function BellOf({
  bell,
  current,
  place,
}: {
  bell: Bell;
  current?: string;
  place?: BellPlace;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const t = useTranslations();
  const failureText = useFailureText();
  const [open, setOpen] = useState(false);
  const [tab, setTab] = useState<NotificationTab>("all");
  const { serverDown, noAnswer } = useServerReach();
  const offline = useOffline(serverDown, noAnswer);
  // A page that failed on GetMe may hold the bell ("No access yet"): reading it again on mount would take that page away.
  const me = useQuery({ ...getGetMeQueryOptions(), retryOnMount: false });
  // The cached list of the tenant layout gives each link its organisation's slug (F30).
  const tenants = useQuery(
    getListMyTenantsQueryOptions({
      pageSize: schemas.listMyTenantsQueryPageSizeMax,
    }),
  );
  const count = useQuery(bell.count);
  const list = useInfiniteQuery({
    ...(tab === "unread" ? bell.unreadList : bell.list),
    enabled: open,
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
    href: notificationHref(item.link, tenants.data?.items ?? [], current),
    organisation: item.tenantName ?? undefined,
    area: item.area,
  });
  const state = stateOf(list, entryOf);

  return (
    <NotificationBell
      unread={count.data?.unread}
      open={open}
      onOpenChange={(next) => {
        if (next) setTab(count.data?.unread ? "unread" : "all");
        setOpen(next);
      }}
      tab={tab}
      onTabChange={setTab}
      list={state}
      onOpenItem={(entry, { follow }) => {
        markRead.mutate(entry.id);
        if (!follow) return;
        setOpen(false);
        router.push(entry.href);
      }}
      onMarkAll={() => markAll.mutate()}
      markingAll={markAll.isPending}
      offline={offline}
      timeZone={me.data?.timeZone}
      linkComponent={Link}
      place={place}
    />
  );
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
