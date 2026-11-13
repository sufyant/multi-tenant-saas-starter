namespace Audit.Application.AuditLogs.GetAuditRetentionPeriod;

/// <summary>The tenant's effective Activity log period at one moment, in days: none for no limit (E3).</summary>
public sealed record AuditRetentionPeriodView(int? Days);
