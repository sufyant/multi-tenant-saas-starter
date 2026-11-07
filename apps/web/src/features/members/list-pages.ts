import {
  getListInvitationsQueryKey,
  getListMembersQueryKey,
  type ListInvitationsParams,
  type ListMembersParams,
  listInvitations,
  listMembers,
} from "@repo/api-client";
import { infiniteQueryOptions } from "@tanstack/react-query";

/** The rows of one page of a list. The API's limit is 1 to 100. */
export const pageSize = 50;

type Page = { page: number; pageSize: number; total: number };

/** The page after the last one, while the pages so far hold fewer rows than the list's `total` (K5). */
const nextPage = (last: Page) =>
  last.page * last.pageSize < last.total ? last.page + 1 : undefined;

/**
 * The members' list as pages that Load more adds to (F9). The key has the shape of the generated one, its operation id and its
 * parameters without the page (F2), so `refreshMembers` reads every loaded page again.
 */
export const memberPagesOptions = (
  tenantId: string,
  params: Omit<ListMembersParams, "page" | "pageSize">,
  enabled: boolean,
) =>
  infiniteQueryOptions({
    queryKey: getListMembersQueryKey(tenantId, { ...params, pageSize }),
    queryFn: ({ pageParam, signal }) =>
      listMembers(
        tenantId,
        { ...params, page: pageParam, pageSize },
        { signal },
      ),
    initialPageParam: 1,
    getNextPageParam: nextPage,
    enabled,
  });

/** The invitations' list as pages, as `memberPagesOptions`. */
export const invitationPagesOptions = (
  tenantId: string,
  params: Omit<ListInvitationsParams, "page" | "pageSize">,
  enabled: boolean,
) =>
  infiniteQueryOptions({
    queryKey: getListInvitationsQueryKey(tenantId, { ...params, pageSize }),
    queryFn: ({ pageParam, signal }) =>
      listInvitations(
        tenantId,
        { ...params, page: pageParam, pageSize },
        { signal },
      ),
    initialPageParam: 1,
    getNextPageParam: nextPage,
    enabled,
  });
