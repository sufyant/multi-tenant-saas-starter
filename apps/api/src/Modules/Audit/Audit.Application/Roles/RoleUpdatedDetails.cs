using Audit.Application.AuditLogs;

namespace Audit.Application.Roles;

/// <summary>
/// The details of <c>role.updated</c>: the old and the new name, and the permissions the change added and removed, in ordinal order.
/// </summary>
public sealed record RoleUpdatedDetails(string OldName, string NewName, IReadOnlyList<string> PermissionsAdded, IReadOnlyList<string> PermissionsRemoved)
    : AuditDetails;
