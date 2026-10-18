using Audit.Application.AuditLogs;

namespace Audit.Application.Tenants;

/// <summary>The details of <c>tenant.renamed</c>: the tenant's old and new name, in both logs.</summary>
public sealed record TenantRenamedDetails(string OldName, string NewName) : AuditDetails;
