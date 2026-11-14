using SharedKernel;

namespace ControlPlane.Contracts.FeatureFlags;

/// <summary>A system admin listed an organisation in the flag's first rule. Published through the outbox, without a tenant.</summary>
/// <param name="TenantName">The organisation's name at that moment.</param>
/// <param name="ListedBy">The user id of the system admin.</param>
[MessageName("feature-flag-tenant-listed")]
public sealed record FeatureFlagTenantListedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid FlagId, string Key, Guid TenantId, string TenantName, Guid ListedBy);
