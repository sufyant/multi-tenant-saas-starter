using Audit.Application.AuditLogs;

namespace Audit.Application.Tenants;

/// <summary>The details of <c>tenant.suspended</c>: the system admin's reason, in the platform's log only.</summary>
public sealed record TenantSuspendedDetails(string Reason) : AuditDetails;
