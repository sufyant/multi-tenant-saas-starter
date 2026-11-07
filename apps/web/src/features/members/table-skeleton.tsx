import { Skeleton as SkeletonBlock } from "@repo/ui/components/ui/skeleton";

/** The bars of a row, by column: Name, Email, Role, Joined (8.02). */
const bars = ["w-37.5", "w-45", "w-17.5", "w-20"];

/**
 * The first load of a list: ten rows shaped like the table's (8.02), each a bar per column, and the empty actions cell. It says
 * what loads to assistive technology and stays still with reduced motion.
 */
export function TableSkeleton({ label }: { label: string }) {
  return (
    <div
      role="status"
      aria-label={label}
      aria-busy="true"
      className="flex flex-col overflow-hidden rounded-lg border border-border"
    >
      {Array.from({ length: 10 }, (_, row) => (
        <div
          // The rows are identical placeholders that never reorder, so their index is a stable key.
          // biome-ignore lint/suspicious/noArrayIndexKey: see above
          key={row}
          className="flex h-row-height items-center gap-3.5 border-b border-border ps-2.5 pe-2 last:border-b-0"
        >
          {bars.map((width, column) => (
            <span
              // biome-ignore lint/suspicious/noArrayIndexKey: the columns are fixed
              key={column}
              className="min-w-0 flex-1"
            >
              <SkeletonBlock
                className={`h-2.5 ${width} max-w-full rounded-sm [animation-duration:1.4s] motion-reduce:animate-none`}
              />
            </span>
          ))}
          <span className="size-7 shrink-0" />
        </div>
      ))}
    </div>
  );
}
