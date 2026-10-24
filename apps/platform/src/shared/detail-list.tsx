import type { ReactNode } from "react";

/** A read-only card of terms and their values (the OrgDetail board): one row for each term. */
export function DetailList({
  rows,
}: {
  rows: { term: string; value: ReactNode }[];
}) {
  return (
    <dl className="grid max-w-[640px] grid-cols-[minmax(0,9rem)_minmax(0,1fr)] rounded-lg border border-border bg-card px-5 py-1 text-body">
      {rows.map(({ term, value }, index) => (
        <div
          key={term}
          className={
            index > 0
              ? "col-span-2 grid grid-cols-subgrid border-t border-border"
              : "col-span-2 grid grid-cols-subgrid"
          }
        >
          <dt className="flex items-center py-3.5 text-muted-foreground">
            {term}
          </dt>
          <dd className="flex min-w-0 flex-wrap items-center gap-2 py-3.5">
            {value}
          </dd>
        </div>
      ))}
    </dl>
  );
}
