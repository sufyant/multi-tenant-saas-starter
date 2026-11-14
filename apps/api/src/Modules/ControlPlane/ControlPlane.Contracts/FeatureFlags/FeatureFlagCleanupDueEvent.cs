using SharedKernel;

namespace ControlPlane.Contracts.FeatureFlags;

/// <summary>
/// The weekly cleanup of the flags found flags to remove from the code: release flags on for everyone for 30 days, and flags off for every
/// organisation for 30 days (3.07). Its id follows the week, so every copy of one week's event tells the system admins once. Published
/// through the outbox, without a tenant; no person acted, so it writes no audit entry.
/// </summary>
[MessageName("feature-flag-cleanup-due")]
public sealed record FeatureFlagCleanupDueEvent(Guid EventId, DateTimeOffset OccurredAt, int ReadyToRemoveCount, int UnusedCount);
