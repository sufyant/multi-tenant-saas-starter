using ControlPlane.Application.Members;

namespace ControlPlane.Application.Users;

/// <summary>
/// A tenant the user is a member of: its name, slug and status (<c>Active</c>, <c>Suspended</c>, <c>Closed</c>), the role the user
/// holds there, when they joined, and whether they are its last Owner. The suspend dialog warns
/// with it, and the delete refusal names those tenants.
/// </summary>
public sealed record UserMembershipView(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string TenantStatus,
    RoleView Role,
    DateTimeOffset JoinedAt,
    bool LastOwner);
