import type { TableSort } from "./table";

/** For each sortable column, the API's sort value up and down (K5). */
export type SortFields = Record<string, { asc: string; desc: string }>;

/** The table's sort from the URL's value, or null when the value belongs to no column. */
export function sortOf(
  fields: SortFields,
  value: string | null,
): TableSort | null {
  for (const [key, field] of Object.entries(fields)) {
    if (value === field.asc) return { key, desc: false };
    if (value === field.desc) return { key, desc: true };
  }
  return null;
}

/** The URL's value from the table's sort; null for no sort, the API's default. */
export function sortFrom(
  fields: SortFields,
  sort: TableSort | null,
): string | null {
  const field = sort ? fields[sort.key] : undefined;
  if (!sort || !field) return null;
  return sort.desc ? field.desc : field.asc;
}
