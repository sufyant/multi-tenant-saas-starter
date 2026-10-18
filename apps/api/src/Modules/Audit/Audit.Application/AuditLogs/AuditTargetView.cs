namespace Audit.Application.AuditLogs;

/// <summary>
/// What the action was done to: a tenant with its stored name, a user with their real name or null and whether they were deleted, or an
/// invitation, which has no name.
/// </summary>
public sealed record AuditTargetView(string Kind, Guid Id, string? Name, bool? Deleted);
