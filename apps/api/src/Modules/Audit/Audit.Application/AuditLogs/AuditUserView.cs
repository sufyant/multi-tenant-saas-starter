namespace Audit.Application.AuditLogs;

/// <summary>
/// A user that an entry's details point to, such as the new Owner of <c>tenant.owner_made</c>: their id, their real name now or null, and
/// whether they were deleted, named as an actor is (K3).
/// </summary>
public sealed record AuditUserView(Guid UserId, string? DisplayName, bool Deleted);
