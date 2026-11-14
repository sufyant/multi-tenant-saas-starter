using SharedKernel;

namespace ControlPlane.Contracts.FeatureFlags;

/// <summary>The first deploy whose code retires the flag archived it. Published through the outbox, without a tenant; no person acted.</summary>
[MessageName("feature-flag-removed-from-code")]
public sealed record FeatureFlagRemovedFromCodeEvent(Guid EventId, DateTimeOffset OccurredAt, Guid FlagId, string Key, string? Revision);
