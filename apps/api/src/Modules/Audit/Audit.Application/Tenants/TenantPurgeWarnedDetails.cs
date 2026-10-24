using Audit.Application.AuditLogs;

namespace Audit.Application.Tenants;

/// <summary>The details of <c>tenant.purge_warned</c>: the moment of the purge that the Owners were warned of, in both logs.</summary>
public sealed record TenantPurgeWarnedDetails(DateTimeOffset PurgesAt) : AuditDetails;
