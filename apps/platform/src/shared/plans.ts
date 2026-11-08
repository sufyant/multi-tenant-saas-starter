import {
  getGetPlanQueryKey,
  getListPlansInfiniteQueryKey,
  getListPlansQueryKey,
  type ListPlansParams,
  ListPlansSort,
  schemas,
} from "@repo/api-client";
import type { QueryClient } from "@tanstack/react-query";

// What the plan screens, Create organisation, the Plan section and Activity share about the plans.

/** Every plan in one page, by name: a platform has a few plans, and the list has no paging of its own (the PlansList board). */
export const plansQuery: ListPlansParams = {
  pageSize: schemas.listPlansQueryPageSizeMax,
  sort: ListPlansSort.Name,
};

/** The Plans list, in the main menu. */
export const plansPage = "/plans";

/** The page of a plan: its editor. */
export const planPage = (id: string) => `${plansPage}/${id}`;

/**
 * After a plan command, or a refusal that says a plan changed meanwhile, the screen reads again (F47): the plan and the list, both its
 * one page and its pages of Load more (F9).
 */
export const refreshPlans = (queryClient: QueryClient, id?: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getListPlansQueryKey() }),
    queryClient.invalidateQueries({ queryKey: getListPlansInfiniteQueryKey() }),
    id && queryClient.invalidateQueries({ queryKey: getGetPlanQueryKey(id) }),
  ]);
