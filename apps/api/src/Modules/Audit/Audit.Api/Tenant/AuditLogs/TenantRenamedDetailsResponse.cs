namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>tenant.renamed</c>: the tenant's old and new name.</summary>
public sealed record TenantRenamedDetailsResponse(string OldName, string NewName) : AuditDetailsResponse;
