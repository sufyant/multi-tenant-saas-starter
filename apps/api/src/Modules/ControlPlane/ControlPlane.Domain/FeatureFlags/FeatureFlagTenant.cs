namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// An organisation that rule 1 of a flag lists: the flag is on for it. It is the platform's data about the organisation, as an entitlement
/// override is (R6), and it goes with the organisation's purge (docs/flows/tenant-purge.md).
/// </summary>
internal sealed class FeatureFlagTenant
{
    public FeatureFlagTenant(Guid flagId, Guid tenantId, Guid listedBy, DateTimeOffset listedAt)
    {
        FlagId = flagId;
        TenantId = tenantId;
        ListedBy = listedBy;
        ListedAt = listedAt;
    }

    public Guid FlagId { get; private init; }

    public Guid TenantId { get; private init; }

    /// <summary>The user id of the system admin who listed the organisation.</summary>
    public Guid ListedBy { get; private init; }

    public DateTimeOffset ListedAt { get; private init; }
}
