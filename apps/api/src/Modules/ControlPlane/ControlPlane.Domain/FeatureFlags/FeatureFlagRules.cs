namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// The three rules of a flag as they apply to one organisation, in their order: the organisation is listed, its bucket is under the
/// percentage, or the flag is on for everyone else. A flag is on for the organisation when one of them is.
/// </summary>
internal sealed record FeatureFlagRules(string Key, bool Listed, int Percentage, bool EveryoneElse)
{
    public bool IsOnFor(Guid tenantId) => ReasonFor(tenantId) is not null;

    /// <summary>The first rule that turns the flag on for the organisation, or none while it is off.</summary>
    public FeatureFlagReason? ReasonFor(Guid tenantId) =>
        Listed ? FeatureFlagReason.Listed
        : FeatureFlagBucket.Of(Key, tenantId) < Percentage ? FeatureFlagReason.Percentage
        : EveryoneElse ? FeatureFlagReason.Everyone
        : null;
}
