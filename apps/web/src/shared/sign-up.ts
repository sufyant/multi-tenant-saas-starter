"use client";

import {
  getGetMeQueryOptions,
  getGetSignUpQueryOptions,
} from "@repo/api-client";
import { useQuery } from "@tanstack/react-query";

/**
 * Whether the signed-in user may create an organisation of their own, from GetSignUp and GetMe (2.38, 2.39): `off` while the product
 * has sign-up off or either read is not known, `reached` at the limit, which counts the organisations the user created that were not
 * purged, and `open` below it.
 */
export function useOwnOrganisation():
  | { state: "off" }
  | { state: "open" | "reached"; limit: number; created: number } {
  const signUp = useQuery(getGetSignUpQueryOptions());
  const me = useQuery(getGetMeQueryOptions());
  if (!signUp.data?.enabled || !me.data) return { state: "off" };
  const limit = signUp.data.tenantLimit;
  const created = me.data.createdTenantCount;
  return { state: created < limit ? "open" : "reached", limit, created };
}

/** The page of Create your organisation (1.28). */
export const newOrganisationPage = "/new";
