using Audit.Application.AuditLogs;

namespace Audit.Application.Roles;

/// <summary>The details of <c>role.created</c>: the names of the role's permissions, in ordinal order. The target keeps its name.</summary>
public sealed record RoleCreatedDetails(IReadOnlyList<string> Permissions) : AuditDetails;
