import {
  getListInvitationsInfiniteQueryOptions,
  getListMembersInfiniteQueryOptions,
  type ListInvitationsParams,
  type ListMembersParams,
} from "@repo/api-client";
import { nextPage, pageSize } from "@/shared/pages";

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
