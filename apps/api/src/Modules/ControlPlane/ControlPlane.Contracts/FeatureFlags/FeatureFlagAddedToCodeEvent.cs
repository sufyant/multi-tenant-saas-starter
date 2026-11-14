using SharedKernel;

namespace ControlPlane.Contracts.FeatureFlags;

/// <summary>The first deploy whose code defines the flag stored it. Published through the outbox, without a tenant; no person acted.</summary>
/// <param name="Revision">The commit of the deploy, when it names one.</param>
[MessageName("feature-flag-added-to-code")]
public sealed record FeatureFlagAddedToCodeEvent(Guid EventId, DateTimeOffset OccurredAt, Guid FlagId, string Key, string? Revision, bool DefaultValue);
