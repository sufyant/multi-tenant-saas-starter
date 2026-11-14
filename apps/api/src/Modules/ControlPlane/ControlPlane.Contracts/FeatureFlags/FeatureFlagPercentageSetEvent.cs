using SharedKernel;

namespace ControlPlane.Contracts.FeatureFlags;

/// <summary>A system admin changed the percentage of the flag's second rule. Published through the outbox, without a tenant.</summary>
/// <param name="SetBy">The user id of the system admin.</param>
[MessageName("feature-flag-percentage-set")]
public sealed record FeatureFlagPercentageSetEvent(Guid EventId, DateTimeOffset OccurredAt, Guid FlagId, string Key, int From, int To, Guid SetBy);
