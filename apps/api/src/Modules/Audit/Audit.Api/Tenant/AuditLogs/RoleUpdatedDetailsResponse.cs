namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// The details of <c>role.updated</c>: the old and the new name, and the permissions the change added and removed, in ordinal order.
/// </summary>
public sealed record RoleUpdatedDetailsResponse(string OldName, string NewName, IReadOnlyList<string> PermissionsAdded, IReadOnlyList<string> PermissionsRemoved)
    : AuditDetailsResponse;
