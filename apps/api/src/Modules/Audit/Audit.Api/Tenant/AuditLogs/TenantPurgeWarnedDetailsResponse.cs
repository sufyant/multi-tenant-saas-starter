namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>tenant.purge_warned</c>: the moment of the purge that the Owners were warned of.</summary>
public sealed record TenantPurgeWarnedDetailsResponse(DateTimeOffset PurgesAt) : AuditDetailsResponse;
