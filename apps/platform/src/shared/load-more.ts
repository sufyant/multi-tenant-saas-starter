"use client";

import {
  actionFor,
  getGetMeQueryKey,
  ProblemDetailsError,
  problemOf,
} from "@repo/api-client";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { showToast } from "@repo/ui/components/toast";
import { rowsIn } from "@repo/ui/lib/pages";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import {
  type InfiniteData,
  type UseInfiniteQueryResult,
  useQueryClient,
} from "@tanstack/react-query";
import { useState } from "react";

/**
 * The next page of a list that grows with Load more (F9): it tells a screen reader how many rows came. A 401 goes to sign-in (F4); a
 * refusal of the caller's account is GetMe's to show (F47); any other failure keeps the rows that came and says so, in a toast, or
 * in place under the rows when the list's frame draws it there (`failure: "in-place"`, 5.19): `failed` is then true until a try
 * succeeds, and `LoadMore` shows its text and Try again.
 */
export function useLoadMore<Page extends { items: unknown[] }>(
  list: UseInfiniteQueryResult<InfiniteData<Page>>,
  texts: { loaded: (count: number) => string; failed: string },
  { failure = "toast" }: { failure?: "toast" | "in-place" } = {},
) {
  const queryClient = useQueryClient();
  const failureText = useFailureText();
  const [announcement, setAnnouncement] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  const loadMore = async () => {
    const before = rowsIn(list.data) ?? 0;
    setFailed(false);
    const result = await list.fetchNextPage();
    if (!result.isFetchNextPageError) {
      setAnnouncement(texts.loaded((rowsIn(result.data) ?? 0) - before));
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
    if (failure === "in-place") return setFailed(true);
    showToast({
      title: texts.failed,
      description: failureText(problemOf(result.error)),
      tone: "destructive",
    });
  };

  return { announcement, failed, loadMore: () => void loadMore() };
}
