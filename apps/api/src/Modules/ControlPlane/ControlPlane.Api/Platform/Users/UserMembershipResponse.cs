using ControlPlane.Api.Tenant.Members;

namespace ControlPlane.Api.Platform.Users;

/// <summary>
/// A tenant the user is a member of: its id, name, slug and status, the user's role there, when they joined, and whether they are
/// its last Owner. The suspend dialog warns with <c>lastOwner</c>, and the delete refusal names those tenants.
/// </summary>
public sealed record UserMembershipResponse(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    UserMembershipResponseTenantStatus TenantStatus,
    RoleResponse Role,
    DateTimeOffset JoinedAt,
    bool LastOwner);
