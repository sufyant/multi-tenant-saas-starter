// Written by apps/api/scripts/generate-feature-flags.mjs from apps/api/src/Modules/ControlPlane/ControlPlane.Domain/FeatureFlags/FeatureFlagKeys.cs. Do not edit it: run `pnpm generate`.

/** The keys of the feature flags that the code defines (apps/api/docs/flows/feature-flags.md). */
export const FeatureFlagKeys = [] as const;

export type FeatureFlagKey = (typeof FeatureFlagKeys)[number];
