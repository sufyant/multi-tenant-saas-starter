import { Skeleton as SkeletonBlock } from "../ui/skeleton";

export type SkeletonProps = {
  /** Rows shaped like the content that loads. Three are enough for a first load. */
  rows?: number;
  /** What is loading, for assistive technology: "Loading members". */
  label: string;
};

/**
 * Placeholder rows shaped like a list row: an avatar circle, two lines, a status at the end (the Skeleton README). It shows only
 * for the first load; it stays still with reduced motion.
 */
export function Skeleton({ rows = 3, label }: SkeletonProps) {
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
