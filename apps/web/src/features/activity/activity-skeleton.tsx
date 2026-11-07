import { Skeleton as SkeletonBlock } from "@repo/ui/components/ui/skeleton";

/** The bars of the first load (12.03): two days, of three entries and of two, each a sentence and its time. */
const days = [
  ["w-64", "w-80", "w-92"],
  ["w-64", "w-80"],
];

const bar =
  "h-2.5 rounded-sm [animation-duration:1.4s] motion-reduce:animate-none";

/**
 * The first load of the log, shaped like its days (12.03): a day's heading over its card of entries. It says what loads to
 * assistive technology and stays still with reduced motion, as the tables' skeleton does.
 */
export function ActivitySkeleton({ label }: { label: string }) {
  return (
    <div
      role="status"
      aria-label={label}
      aria-busy="true"
      className="flex flex-col gap-1.5"
    >
      {days.map((entries, day) => (
        // The days are identical placeholders that never reorder, so their index is a stable key.
        // biome-ignore lint/suspicious/noArrayIndexKey: see above
        <div key={day} className="flex flex-col gap-1.5">
          <SkeletonBlock className={`mt-2 w-25 ${bar}`} />
          <div className="rounded-lg border border-border bg-card">
            {entries.map((width, entry) => (
              <div
                // biome-ignore lint/suspicious/noArrayIndexKey: the entries are fixed
                key={entry}
                className="flex items-center justify-between gap-4 border-t border-border px-4 py-3 first:border-t-0"
              >
                <SkeletonBlock className={`${width} max-w-full ${bar}`} />
                <SkeletonBlock className={`w-7 shrink-0 ${bar}`} />
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
