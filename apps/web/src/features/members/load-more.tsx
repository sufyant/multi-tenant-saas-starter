"use client";

import { Button } from "@repo/ui/components/button";
import { Skeleton as SkeletonBlock } from "@repo/ui/components/ui/skeleton";
import { useEffect, useRef } from "react";

/**
 * The foot of a list that grows (F9): "Load more" while a next page exists; while it comes, a "Loading more …" row in the
 * button's place (8.05). A polite live region says how many rows came (WCAG 2.2, 4.1.3); it stays in the page, so a screen
 * reader hears each change. The button takes the focus back when it returns, so a keyboard user does not start again at the top.
 */
export function LoadMore({
  hasMore,
  loading,
  onMore,
  label,
  loadingLabel,
  announcement,
}: {
  hasMore: boolean;
  loading: boolean;
  onMore: () => void;
  label: string;
  loadingLabel: string;
  /** What came with the last page: "10 more members loaded.", or nothing yet. */
  announcement: string | null;
}) {
  const button = useRef<HTMLButtonElement>(null);
  const refocus = useRef(false);
  useEffect(() => {
    if (loading || !refocus.current) return;
    refocus.current = false;
    button.current?.focus();
  }, [loading]);
  return (
    <>
      <p aria-live="polite" className="sr-only">
        {announcement}
      </p>
      {loading && (
        <div className="flex h-row-height items-center gap-2.5 ps-11">
          <SkeletonBlock className="h-2.5 w-40 rounded-sm [animation-duration:1.4s] motion-reduce:animate-none" />
          <span className="text-small text-muted-foreground">
            {loadingLabel}
          </span>
        </div>
      )}
      {hasMore && !loading && (
        <div className="flex justify-center py-2">
          <Button
            ref={button}
            onClick={() => {
              refocus.current = document.activeElement === button.current;
              onMore();
            }}
          >
            {label}
          </Button>
        </div>
      )}
    </>
  );
}
