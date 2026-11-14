"use client";

import {
  type FeatureFlagKey,
  getGetMyTenantFeatureFlagsQueryOptions,
} from "@repo/api-client";
import { useQuery } from "@tanstack/react-query";
import { useTenant } from "./tenant-context";

/**
 * Whether a feature flag is on for the organisation of the route (GetMyTenantFeatureFlags, F65): `useFeatureFlag("roles.bulk_edit")`.
 * One read answers every flag. It reads on page load and again on window focus once it is a minute old, so a change reaches an
 * open page within a minute. `undefined` until the first answer: the screen waits, as for any other read. The API answers each
 * flag's default value when its own read fails.
 */
export function useFeatureFlag(key: FeatureFlagKey): boolean | undefined {
  const { tenant } = useTenant();
  const flags = useQuery(
    getGetMyTenantFeatureFlagsQueryOptions(tenant.id, {
      query: { staleTime: 60_000, refetchOnWindowFocus: true },
    }),
  );
  return flags.data?.flags[key];
}
