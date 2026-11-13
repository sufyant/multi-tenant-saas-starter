namespace Audit.Application.AuditLogs.GetAuditRetention;

/// <summary>The period of a running hold, in days, and the moment its held entries go.</summary>
public sealed record AuditRetentionShorteningView(int Days, DateTimeOffset GoesAt);
