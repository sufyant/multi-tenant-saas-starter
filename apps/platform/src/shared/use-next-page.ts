"use client";

import {
  actionFor,
  getGetMeQueryKey,
  ProblemDetailsError,
} from "@repo/api-client";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { rowsIn } from "@repo/ui/lib/pages";
import {
  type InfiniteData,
  type UseInfiniteQueryResult,
  useQueryClient,
} from "@tanstack/react-query";
import { useState } from "react";

/**
 * The next page of a list that grows with Load more (F9), for a list that says a failed page under its rows (5.19): what came,
 * for the live region, and whether the last try failed. A 401 goes to sign-in (F4); a refusal of the caller's account is GetMe's
 * to show (F47).
 */
export function useNextPage<Page extends { items: unknown[] }>(
  list: UseInfiniteQueryResult<InfiniteData<Page>>,
  loaded: (count: number) => string,
) {
  const queryClient = useQueryClient();
  const [announcement, setAnnouncement] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  const more = async () => {
    const before = rowsIn(list.data) ?? 0;
    setFailed(false);
    const result = await list.fetchNextPage();
    if (!result.isFetchNextPageError) {
      setAnnouncement(loaded((rowsIn(result.data) ?? 0) - before));
      return;
    }
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
    setFailed(true);
  };

  return { announcement, failed, more };
}
