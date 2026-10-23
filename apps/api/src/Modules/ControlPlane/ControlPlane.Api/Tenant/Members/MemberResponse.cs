using ControlPlane.Api.Tenant.Roles;

namespace ControlPlane.Api.Tenant.Members;

/// <summary>
/// A member of the tenant: the user's id, the copy of their email address and name, their role, whether the identity provider deleted
/// the user, and when they joined the tenant. A deleted user has an empty email address and empty names, and
/// the screen shows "Deleted user", as the audit logs say <c>deleted</c> (K3).
/// </summary>
public sealed record MemberResponse(Guid UserId, string? Email, string? FirstName, string? LastName, RoleResponse Role, bool Deleted, DateTimeOffset JoinedAt);
