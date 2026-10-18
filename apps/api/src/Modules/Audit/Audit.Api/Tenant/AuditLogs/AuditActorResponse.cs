namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// Who acted: a user, by catalog user id, or the platform, that is a system admin seen from a tenant's log. <c>displayName</c> is a
/// user's real name, or null: for a user without a name, a deleted user, a user the catalog does not know, and for the platform, which
/// the screen names from <c>kind</c> in its own language. <c>deleted</c> is set for a user only.
/// </summary>
public sealed record AuditActorResponse(AuditActorResponseKind Kind, Guid? UserId, string? DisplayName, bool? Deleted);
