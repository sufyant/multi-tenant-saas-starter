namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// The three rules of one flag for every organisation at once: the listed organisations, the percentage and everyone else. A preview
/// compares the setting before a change with the setting after it, organisation by organisation.
/// </summary>
internal sealed record FeatureFlagSetting(string Key, IReadOnlySet<Guid> Listed, int Percentage, bool EveryoneElse)
{
    public bool IsOnFor(Guid tenantId) => new FeatureFlagRules(Key, Listed.Contains(tenantId), Percentage, EveryoneElse).IsOnFor(tenantId);

    /// <summary>
    /// How many of these organisations a change from this setting to the other one turns on or off: the count that a confirmation names
    /// before the change (12.06, 12.08, 12.09). It never blocks the change.
    /// </summary>
    public int AffectedBy(FeatureFlagSetting after, IEnumerable<Guid> tenantIds) => tenantIds.Count(tenantId => IsOnFor(tenantId) != after.IsOnFor(tenantId));
}
