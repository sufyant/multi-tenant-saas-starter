namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// The tenant's Activity log period, in days, null for no limit, and the shortening that the Banner shows: null unless a hold runs and an
/// entry it keeps is older than the period.
/// </summary>
public sealed record AuditRetentionResponse(int? Days, AuditRetentionShorteningResponse? Shortening);
