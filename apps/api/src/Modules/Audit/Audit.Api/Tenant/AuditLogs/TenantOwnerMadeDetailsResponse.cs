namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// The details of <c>tenant.owner_made</c>: the member who became an Owner, and the role they had. The entry keeps the member by id only
/// (K3); <c>displayName</c> and <c>deleted</c> name them now, as an actor is named: <c>displayName</c> is the real name, or null for a
/// user without a name, a deleted user and an unknown user, and <c>deleted</c> is true for a deleted user.
/// </summary>
public sealed record TenantOwnerMadeDetailsResponse(Guid UserId, string? DisplayName, bool Deleted, AuditRoleResponse OldRole) : AuditDetailsResponse;
