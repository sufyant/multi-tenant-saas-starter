namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// What the action was done to: a tenant or a role with its name at that moment, a user with their real name now or null, or an
/// invitation, which has no name. <c>deleted</c> is set for a user only.
/// </summary>
public sealed record AuditTargetResponse(AuditTargetResponseKind Kind, Guid Id, string? Name, bool? Deleted);
