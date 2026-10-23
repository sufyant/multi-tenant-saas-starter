namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>tenant.owner_made</c>: the member who became an Owner, and the role they had.</summary>
public sealed record TenantOwnerMadeDetailsResponse(Guid UserId, AuditRoleResponse OldRole) : AuditDetailsResponse;
