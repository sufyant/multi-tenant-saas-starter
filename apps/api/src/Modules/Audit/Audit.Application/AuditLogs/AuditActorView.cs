namespace Audit.Application.AuditLogs;

/// <summary>
/// Who acted: a user carries their user id, their real name or null, and whether they were deleted. The platform carries only
/// its kind.
/// </summary>
public sealed record AuditActorView(string Kind, Guid? UserId, string? DisplayName, bool? Deleted);
