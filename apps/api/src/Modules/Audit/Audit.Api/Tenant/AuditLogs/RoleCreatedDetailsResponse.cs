namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>role.created</c>: the names of the new role's permissions, in ordinal order.</summary>
public sealed record RoleCreatedDetailsResponse(IReadOnlyList<string> Permissions) : AuditDetailsResponse;
