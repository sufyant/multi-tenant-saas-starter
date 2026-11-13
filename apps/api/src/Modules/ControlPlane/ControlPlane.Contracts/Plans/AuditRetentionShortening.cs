namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A tenant's Activity log period got shorter at the event's moment (K14): from <paramref name="FromDays"/>, none for no limit, to
/// <paramref name="ToDays"/>. The entries that the period before kept stay until <paramref name="GoesAt"/>, the event's moment plus
/// <see cref="AuditRetention.Grace"/> days, then go.
/// </summary>
public sealed record AuditRetentionShortening(int? FromDays, int ToDays, DateTimeOffset GoesAt);
