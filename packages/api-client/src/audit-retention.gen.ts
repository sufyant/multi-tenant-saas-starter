// Written by apps/api/scripts/generate-entitlements.mjs from apps/api/src/Modules/ControlPlane/ControlPlane.Domain/Plans/AuditRetention.cs. Do not edit it: run `pnpm generate`.

/**
 * The days of the tenant's Activity log period: Floor, the shortest period, which the deletion also keeps, and Grace, the hold
 * of a shorter period (apps/api/docs/ARCHITECTURE.md, K14 and R19).
 */
export const AuditRetention = {
  Floor: 30,
  Grace: 30,
} as const;
