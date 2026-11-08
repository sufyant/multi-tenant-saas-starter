"use client";

import {
  type ColumnDef,
  type PaginationState,
  type RowData,
  rowPaginationFeature,
  rowSortingFeature,
  type SortingState,
  tableFeatures,
  type Updater,
  useTable,
} from "@tanstack/react-table";
import type { ReactNode } from "react";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import { ChevronDownIcon, Icon } from "../icon";
import {
  Table as TableBase,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "../ui/table";

export type TableColumn<Row extends RowData> = {
  key: string;
  header: ReactNode;
  /** Numbers align to the end. */
  align?: "start" | "end";
  /** A supporting column, such as "Last active", in the muted colour. */
  secondary?: boolean;
  /** The column is a sort field of the list (K5 in api-catalog). */
  sortable?: boolean;
  render?: (row: Row) => ReactNode;
  /** Its place on the phone card. Default: the first column is the title, the others are the meta line. */
  card?: "title" | "meta" | "end" | "hide";
  /** A shorter cell for the phone card, such as the name without the email. */
  cardRender?: (row: Row) => ReactNode;
  /** wide: the column shows only from `cq-table-wide` of the table's own width, such as "Joined" on a tablet. The card ignores it. */
  show?: "wide";
  /** The width class its frame gives the column, such as `w-62.5` for 250 px. The first column has none: it fills (Table row, 424:2472). */
  width?: string;
};

export type TableSort = { key: string; desc: boolean };

export type TableProps<Row extends RowData> = {
  columns: TableColumn<Row>[];
  /** One page of rows, as the API answered it. */
  rows: Row[];
  rowKey: keyof Row & string;
  /** The accessible name of the table and of the phone card list. */
  label: string;
  /** One RowMenu per row. */
  actions?: (row: Row) => ReactNode;
  actionsLabel?: string;
  /** The leading element of a phone card, usually the avatar. */
  cardLead?: (row: Row) => ReactNode;
  /** The API's `total`: the table pages on the server (manual mode). */
  rowCount: number;
  /** The page, from 1, and its size. */
  page: number;
  pageSize: number;
  onPageChange?: (page: number) => void;
  sort?: TableSort | null;
  onSortChange?: (sort: TableSort | null) => void;
  /** The labels of the pager: the two buttons and "Page 1 of 3". */
  pager: {
    previous: string;
    next: string;
    page: (page: number, pageCount: number) => string;
  };
  className?: string;
};

const features = tableFeatures({ rowPaginationFeature, rowSortingFeature });

const apply = <T,>(updater: Updater<T>, old: T): T =>
  typeof updater === "function" ? (updater as (old: T) => T)(old) : updater;

const cellText = <Row extends RowData>(
  column: TableColumn<Row>,
  row: Row,
): ReactNode =>
  column.render
    ? column.render(row)
    : String((row as Record<string, unknown>)[column.key] ?? "");

/** The cells' padding (Table row, 424:2472): 10 at the start of the row, 14 between cells, 8 at the end. */
const cellPadding = (index: number, last: boolean) =>
  cn(index === 0 ? "ps-2.5" : "ps-3.5", last ? "pe-2" : "pe-0");

/**
 * A list of records (the Table README and the Table component, 424:2473) on TanStack Table in manual mode: the API pages and
 * sorts, the table shows one page. A row's ⋯ shows on hover, on focus, while its menu is open and always on a touch screen
 * (Table · Actions, 424:2404). Under 640px of container width it becomes a card list (Table · Card, 258:1389): lead, title, one
 * meta line joined by "·", the status at the end, then ⋯, always shown.
 */
export function Table<Row extends RowData>({
  columns,
  rows,
  rowKey,
  label,
  actions,
  actionsLabel,
  cardLead,
  rowCount,
  page,
  pageSize,
  onPageChange,
  sort,
  onSortChange,
  pager,
  className,
}: TableProps<Row>) {
  const sorting: SortingState = sort ? [{ id: sort.key, desc: sort.desc }] : [];
  const pagination: PaginationState = { pageIndex: page - 1, pageSize };
  const table = useTable({
    features,
    columns: columns.map((column) => ({
      id: column.key,
      accessorFn: (row: Row) => (row as Record<string, unknown>)[column.key],
      enableSorting: column.sortable ?? false,
      // The first click sorts up, whatever the cell's value: a column of a rendered person has no string to guess from.
      sortDescFirst: false,
    })) as ColumnDef<typeof features, Row>[],
    data: rows,
    getRowId: (row: Row) => String(row[rowKey]),
    manualPagination: true,
    manualSorting: true,
    rowCount,
    state: { sorting, pagination },
    onSortingChange: (updater: Updater<SortingState>) => {
      const [next] = apply(updater, sorting);
      onSortChange?.(next ? { key: next.id, desc: next.desc } : null);
    },
    onPaginationChange: (updater: Updater<PaginationState>) =>
      onPageChange?.(apply(updater, pagination).pageIndex + 1),
  });
  const pageCount = Math.max(1, table.getPageCount());
  const placeOf = (column: TableColumn<Row>, index: number) =>
    column.card ?? (index === 0 ? "title" : "meta");

  const lastColumn = columns.length - 1;
  return (
    <div className={cn("@container flex flex-col gap-3", className)}>
      <div className="hidden overflow-hidden rounded-lg border border-border bg-card @min-[640px]:block">
        <TableBase
          aria-label={label}
          className="hidden text-body tabular-nums @min-[640px]:table"
        >
          <TableHeader>
            <TableRow className="h-row-header border-border bg-muted hover:bg-muted">
              {table.getHeaderGroups()[0]?.headers.map((header, index) => {
                const column = columns[index] as TableColumn<Row>;
                const direction = header.column.getIsSorted();
                return (
                  <TableHead
                    key={header.id}
                    aria-sort={
                      direction
                        ? direction === "desc"
                          ? "descending"
                          : "ascending"
                        : undefined
                    }
                    className={cn(
                      "h-row-header text-status text-muted-foreground",
                      cellPadding(index, !actions && index === lastColumn),
                      column.width,
                      column.align === "end" && "text-end",
                      column.show === "wide" && "hidden @table-wide:table-cell",
                    )}
                  >
                    {column.sortable ? (
                      <button
                        type="button"
                        onClick={header.column.getToggleSortingHandler()}
                        className="inline-flex items-center gap-1 hover:text-foreground"
                      >
                        {column.header}
                        {direction && (
                          <Icon
                            icon={ChevronDownIcon}
                            size={14}
                            className={cn(direction === "asc" && "rotate-180")}
                          />
                        )}
                      </button>
                    ) : (
                      column.header
                    )}
                  </TableHead>
                );
              })}
              {actions && (
                <TableHead className="w-target-row-action ps-3.5 pe-2">
                  {actionsLabel && (
                    <span className="sr-only">{actionsLabel}</span>
                  )}
                </TableHead>
              )}
            </TableRow>
          </TableHeader>
          <TableBody>
            {table.getRowModel().rows.map((row) => (
              <TableRow
                key={row.id}
                className="group/row h-row-height border-border focus-within:bg-row-hover"
              >
                {columns.map((column, index) => (
                  <TableCell
                    key={column.key}
                    className={cn(
                      "py-0",
                      cellPadding(index, !actions && index === lastColumn),
                      column.align === "end" && "text-end",
                      column.secondary && "text-small text-muted-foreground",
                      column.show === "wide" && "hidden @table-wide:table-cell",
                    )}
                  >
                    {cellText(column, row.original)}
                  </TableCell>
                ))}
                {actions && (
                  <TableCell className="py-0 ps-3.5 pe-2 text-end">
                    {/* The ⋯ button keeps its cell at rest, and a text in its place always shows; a fine pointer gets the 28 px
                      button, a touch screen keeps its touch size. */}
                    <div className="flex justify-end [&>button]:opacity-0 group-hover/row:[&>button]:opacity-100 focus-within:[&>button]:opacity-100 [&>button]:data-popup-open:opacity-100 pointer-coarse:[&>button]:opacity-100 pointer-fine:[&>button]:size-target-row-action">
                      {actions(row.original)}
                    </div>
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </TableBase>
      </div>

      <ul aria-label={label} className="flex flex-col @min-[640px]:hidden">
        {rows.map((row) => {
          const card = (place: string) =>
            columns
              .filter((column, index) => placeOf(column, index) === place)
              .map((column) => ({
                column,
                cell: column.cardRender
                  ? column.cardRender(row)
                  : cellText(column, row),
              }));
          return (
            <li
              key={String(row[rowKey])}
              className="flex min-h-16 items-center gap-3 border-b border-border bg-card px-4 py-3"
            >
              {cardLead?.(row)}
              <div className="flex min-w-0 flex-1 flex-col">
                {card("title").map(({ column, cell }) => (
                  <span
                    key={column.key}
                    className="truncate text-body font-medium"
                  >
                    {cell}
                  </span>
                ))}
                <span className="truncate text-small text-muted-foreground">
                  {card("meta").map(({ column, cell }, index) => (
                    <span key={column.key}>
                      {index > 0 && " · "}
                      {cell}
                    </span>
                  ))}
                </span>
              </div>
              {card("end").map(({ column, cell }) => (
                <span key={column.key}>{cell}</span>
              ))}
              {actions && (
                <div className="flex [&>button]:size-touch-target [&>button_svg]:size-5">
                  {actions(row)}
                </div>
              )}
            </li>
          );
        })}
      </ul>

      {pageCount > 1 && (
        <div className="flex items-center justify-end gap-2 text-small text-muted-foreground">
          <span>{pager.page(page, pageCount)}</span>
          <Button
            onClick={() => table.previousPage()}
            disabled={!table.getCanPreviousPage()}
          >
            {pager.previous}
          </Button>
          <Button
            onClick={() => table.nextPage()}
            disabled={!table.getCanNextPage()}
          >
            {pager.next}
          </Button>
        </div>
      )}
    </div>
  );
}
