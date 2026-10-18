using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Users;

/// <summary>
/// A user as the system admin sees them: the copy of their profile, their status (Active, Suspended or Deleted), whether they
/// are a system admin, how many tenants they are a member of (R13) and when they became a user. The id is ours, not the
/// identity provider's.
/// </summary>
public sealed record UserView(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    string Status,
    bool IsSystemAdmin,
    int MembershipCount,
    DateTimeOffset CreatedAt)
{
    internal static UserView Of(User user, bool isSystemAdmin) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.Status.ToString(), isSystemAdmin, user.MembershipCount, user.CreatedAt);
}
