import { Skeleton as SkeletonBlock } from "@repo/ui/components/ui/skeleton";

/** The bars of a row, by column, as 8.02 draws Name, Email, Role and Joined; a list with more columns repeats them. */
const bars = ["w-37.5", "w-45", "w-17.5", "w-20"];

/**
 * The first load of a list: the table's header with its columns, then ten rows shaped like the table's (8.02), each a bar per
 * column, and the empty actions cell. It says what loads to assistive technology and stays still with reduced motion.
 */
export function TableSkeleton({
  label,
  columns,
}: {
  label: string;
  /** The table's column names, as its header shows them. */
  columns: readonly string[];
}) {
  return (
    <div
      role="status"
      aria-label={label}
      aria-busy="true"
      className="flex flex-col overflow-hidden rounded-lg border border-border"
    >
      <div
        aria-hidden="true"
        className="flex h-row-header items-center gap-3.5 border-b border-border bg-muted ps-2.5 pe-2 text-status text-muted-foreground"
      >
        {columns.map((column) => (
          <span key={column} className="min-w-0 flex-1 truncate">
            {column}
          </span>
        ))}
        <span className="size-7 shrink-0" />
      </div>
      {Array.from({ length: 10 }, (_, row) => (
        <div
          // The rows are identical placeholders that never reorder, so their index is a stable key.
          // biome-ignore lint/suspicious/noArrayIndexKey: see above
          key={row}
          className="flex h-row-height items-center gap-3.5 border-b border-border ps-2.5 pe-2 last:border-b-0"
        >
          {columns.map((column, index) => (
            <span key={column} className="min-w-0 flex-1">
              <SkeletonBlock
                className={`h-2.5 ${bars[index % bars.length]} max-w-full rounded-sm [animation-duration:1.4s] motion-reduce:animate-none`}
              />
            </span>
          ))}
          <span className="size-7 shrink-0" />
        </div>
      ))}
    </div>
  );
}
