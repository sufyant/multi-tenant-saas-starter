// The lists that grow with Load more (F9): Members, Invitations and Roles by page, the Activity log by cursor.

/** The rows of one page of a list. The API's limit is 1 to 100. */
export const pageSize = 50;

type Page = { page: number; pageSize: number; total: number };

/** The page after the last one, while the pages so far hold fewer rows than the list's `total` (K5). */
export const nextPage = (last: Page) =>
  last.page * last.pageSize < last.total ? last.page + 1 : undefined;

/** The rows that the pages so far hold, or none before the first page. */
export const rowsIn = (data: { pages: { items: unknown[] }[] } | undefined) =>
  data?.pages.reduce((count, page) => count + page.items.length, 0);
