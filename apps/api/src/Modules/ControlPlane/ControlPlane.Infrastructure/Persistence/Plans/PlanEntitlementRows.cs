namespace ControlPlane.Infrastructure.Persistence.Plans;

/// <summary>What the cache keeps of a plan (C4, <see cref="CachedReads.PlanEntitlements"/>): its value of each key it has one for.</summary>
internal sealed record PlanEntitlementRows(EntitlementRow[] Values);
