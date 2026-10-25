"use client";

import { useEffect, useState } from "react";
import { Skeleton as SkeletonBlock } from "../ui/skeleton";

/**
 * How long a delayed skeleton waits before it shows. An answer within it shows no skeleton at all, so a fast load does not
 * flash. Below about 0.1 s a response feels instant (Nielsen, "Response Times: The 3 Important Limits"); a placeholder that
 * comes and goes within a fifth of a second reads as a flicker, not as progress.
 */
export const skeletonDelayMs = 200;

/** True once the skeleton delay has passed since the first render. */
function useDelayPassed(delayed: boolean): boolean {
  const [passed, setPassed] = useState(!delayed);
  useEffect(() => {
    if (passed) return;
    const timer = setTimeout(() => setPassed(true), skeletonDelayMs);
    return () => clearTimeout(timer);
  }, [passed]);
  return passed;
}

export type SkeletonProps = {
  /** Rows shaped like the content that loads. Three are enough for a first load. */
  rows?: number;
  /** What is loading, for assistive technology: "Loading members". */
  label: string;
  /** Show nothing until the skeleton delay has passed, so a fast answer shows no flash. */
  delayed?: boolean;
};

/**
 * Placeholder rows shaped like a list row: an avatar circle, two lines, a status at the end (the Skeleton README). It shows only
 * for the first load; it stays still with reduced motion.
 */
export function Skeleton({ rows = 3, label, delayed = false }: SkeletonProps) {
  const passed = useDelayPassed(delayed);
  if (!passed) return null;
  return (
    <div
      role="status"
      aria-label={label}
      aria-busy="true"
      className="flex flex-col"
    >
      {Array.from({ length: rows }, (_, row) => (
        <div
          // The rows are identical placeholders that never reorder, so their index is a stable key.
          // biome-ignore lint/suspicious/noArrayIndexKey: see above
          key={row}
          className="flex h-14 items-center gap-3 border-b border-border"
        >
          <SkeletonBlock className="size-8 rounded-full [animation-duration:1.4s] motion-reduce:animate-none" />
          <div className="flex flex-1 flex-col gap-2">
            <SkeletonBlock className="h-3 w-2/5 [animation-duration:1.4s] motion-reduce:animate-none" />
            <SkeletonBlock className="h-3 w-3/5 [animation-duration:1.4s] motion-reduce:animate-none" />
          </div>
          <SkeletonBlock className="h-3 w-16 [animation-duration:1.4s] motion-reduce:animate-none" />
        </div>
      ))}
    </div>
  );
}

/**
 * A short line where a name will be, inside a bar that is already drawn: the organisation's name in the top bar. It waits for the
 * skeleton delay and holds its place until then. Assistive technology skips it, because the page's own Skeleton says what is loading.
 */
export function InlineSkeleton() {
  const passed = useDelayPassed(true);
  // Before the delay an empty box of the same size holds the place, so the bar does not move when the skeleton comes.
  if (!passed) return <span aria-hidden="true" className="block h-3 w-24" />;
  return (
    <SkeletonBlock
      aria-hidden="true"
      className="h-3 w-24 [animation-duration:1.4s] motion-reduce:animate-none"
    />
  );
}
