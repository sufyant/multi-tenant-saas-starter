using ControlPlane.Application.SystemAdmins;
using ControlPlane.Domain.SystemAdmins;
using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Users;

/// <summary>
/// A user as the system admin sees them: the copy of their profile, their status (Active, Suspended or Deleted), whether they
/// are a system admin and whether a root one, how many tenants they are a member of (R13) and when they became a user. The id is ours,
/// not the identity provider's.
/// </summary>
public sealed record UserView(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    string Status,
    bool IsSystemAdmin,
    bool IsRootSystemAdmin,
    int MembershipCount,
    DateTimeOffset CreatedAt)
{
    /// <summary>The view of a user. A root is a system admin whose address the list names, read from the list now (SystemDoor.IsRoot).</summary>
    internal static UserView Of(User user, bool isSystemAdmin, SystemAdminOptions systemAdmins) =>
        new(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Status.ToString(),
            isSystemAdmin,
            isSystemAdmin && SystemDoor.IsRoot(user.Email, systemAdmins.Roots),
            user.MembershipCount,
            user.CreatedAt);
}
