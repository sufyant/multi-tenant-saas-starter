namespace ControlPlane.Infrastructure.Persistence.FeatureFlags;

/// <summary>
/// What the cache keeps of the flags for one organisation (C4, <see cref="CachedReads.TenantFeatureFlags"/>): each stored flag the code
/// has not removed, with its rules as the rows say them for the organisation. The bucket of rule 2 is computed at each read, never stored.
/// </summary>
internal sealed record TenantFeatureFlagRows(FeatureFlagRuleRow[] Flags);
