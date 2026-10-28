namespace ControlPlane.Infrastructure.Persistence.Plans;

/// <summary>
/// What the cache keeps of a tenant's entitlements (C4, <see cref="CachedReads.TenantEntitlements"/>): its plan, and its overrides with
/// their expiry. Whether an override applies is computed at each read, never stored.
/// </summary>
internal sealed record TenantEntitlementRows(Guid PlanId, EntitlementRow[] Overrides);
