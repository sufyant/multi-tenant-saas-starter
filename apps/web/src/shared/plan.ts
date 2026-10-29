import type {
  FeatureKey,
  LimitKey,
  MyTenantDetailResponse,
} from "@repo/api-client";
import { useTenant } from "./tenant-context";

type Plan = Pick<MyTenantDetailResponse, "entitlements" | "usage">;

/** A limit as the screens read it: the value (null is unlimited), the tenant's count, and where the count stands. */
export type LimitState = {
  limit: number | null;
  count: number;
  /** The count is the limit. */
  atLimit: boolean;
  /** The count is above the limit, after a limit lowered later (E4). */
  overLimit: boolean;
  /** Nothing new that counts can be added: at or over the limit. */
  full: boolean;
};

/** True when the feature is on. A key without a value is closed (E3). */
export function featureOn(plan: Plan, key: FeatureKey): boolean {
  return plan.entitlements[key] === true;
}

/** A limit's value and the tenant's count (`GetMyTenant.usage`). Null is unlimited; a key without a value is closed at 0 (E3). */
export function limitOf(plan: Plan, key: LimitKey): LimitState {
  const value = plan.entitlements[key];
  const limit = value === null ? null : typeof value === "number" ? value : 0;
  const count = plan.usage[key] ?? 0;
  const atLimit = limit !== null && count === limit;
  const overLimit = limit !== null && count > limit;
  return { limit, count, atLimit, overLimit, full: atLimit || overLimit };
}

/**
 * The tenant's plan from GetMyTenant: whether a feature is on, and where a limit stands. Every screen asks it; no screen compares
 * the numbers itself. The API still checks each feature and limit (E5, E6).
 */
export function usePlan(): {
  on: (key: FeatureKey) => boolean;
  limit: (key: LimitKey) => LimitState;
} {
  const { detail } = useTenant();
  return {
    on: (key) => featureOn(detail, key),
    limit: (key) => limitOf(detail, key),
  };
}
