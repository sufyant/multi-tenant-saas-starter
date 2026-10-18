namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>tenant.suspended</c>: the system admin's reason. Only the platform's log holds it.</summary>
public sealed record TenantSuspendedDetailsResponse(string Reason) : AuditDetailsResponse;
