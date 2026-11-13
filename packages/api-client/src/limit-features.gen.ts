// Written by apps/api/scripts/generate-entitlements.mjs from apps/api/src/Modules/ControlPlane/ControlPlane.Domain/Plans/LimitKeys.cs. Do not edit it: run `pnpm generate`.

/**
 * The feature that each limit depends on: the limit changes only while the feature is on (apps/api/docs/ARCHITECTURE.md, E1). A
 * limit that is not here depends on no feature.
 */
export const LimitFeatures = {
  "roles.custom.max": "roles.custom",
} as const;
