using SharedKernel;

namespace ControlPlane.Contracts.FeatureFlags;

/// <summary>A system admin turned the flag on or off for everyone else, its third rule. Published through the outbox, without a tenant.</summary>
/// <param name="SetBy">The user id of the system admin.</param>
[MessageName("feature-flag-everyone-else-set")]
public sealed record FeatureFlagEveryoneElseSetEvent(Guid EventId, DateTimeOffset OccurredAt, Guid FlagId, string Key, bool On, Guid SetBy);
