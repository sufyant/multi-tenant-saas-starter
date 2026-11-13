namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The period of a running hold, in days, and the moment the entries older than it go, at the hold's end.</summary>
public sealed record AuditRetentionShorteningResponse(int Days, DateTimeOffset GoesAt);
