import {
  getListInvitationsInfiniteQueryOptions,
  getListMembersInfiniteQueryOptions,
  type ListInvitationsParams,
  type ListMembersParams,
} from "@repo/api-client";

/** The rows of one page of a list. The API's limit is 1 to 100. */
export const pageSize = 50;

type Page = { page: number; pageSize: number; total: number };

/** The page after the last one, while the pages so far hold fewer rows than the list's `total` (K5). */
const nextPage = (last: Page) =>
  last.page * last.pageSize < last.total ? last.page + 1 : undefined;

/**
 * The members' list as pages that Load more adds to (F9): Orval's infinite options, whose page parameter is `page` (F2), as the
 * Activity log's are with its cursor. `refreshMembers` invalidates their generated key.
 */
export const memberPagesOptions = (
  tenantId: string,
  params: Omit<ListMembersParams, "page" | "pageSize">,
  enabled: boolean,
) =>
  getListMembersInfiniteQueryOptions(
    tenantId,
    { ...params, pageSize },
    { query: { initialPageParam: 1, getNextPageParam: nextPage, enabled } },
  );

/** The invitations' list as pages, as `memberPagesOptions`. */
export const invitationPagesOptions = (
  tenantId: string,
  params: Omit<ListInvitationsParams, "page" | "pageSize">,
  enabled: boolean,
) =>
  getListInvitationsInfiniteQueryOptions(
    tenantId,
    { ...params, pageSize },
    { query: { initialPageParam: 1, getNextPageParam: nextPage, enabled } },
  );
