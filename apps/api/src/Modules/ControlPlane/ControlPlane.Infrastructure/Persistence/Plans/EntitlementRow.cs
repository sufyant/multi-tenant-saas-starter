namespace ControlPlane.Infrastructure.Persistence.Plans;

/// <summary>A stored value of one key as the cache keeps it (C4): the row's columns, and an override's expiry.</summary>
internal sealed record EntitlementRow(string Key, bool? Enabled, int? Maximum, bool Unlimited, DateTimeOffset? ExpiresAt);
