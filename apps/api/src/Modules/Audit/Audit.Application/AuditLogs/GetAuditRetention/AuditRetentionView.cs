namespace Audit.Application.AuditLogs.GetAuditRetention;

/// <summary>
/// The tenant's Activity log period, in days, none for no limit, and the entries that go when a hold ends: none unless a hold runs and an
/// entry it keeps is older than the period.
/// </summary>
public sealed record AuditRetentionView(int? Days, AuditRetentionShorteningView? Shortening);
