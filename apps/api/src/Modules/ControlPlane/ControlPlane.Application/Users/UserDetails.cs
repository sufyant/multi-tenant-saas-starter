using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Users;

/// <summary>
/// A catalog user as the system admin sees them: the copy of their profile, their status (Active, Suspended or Deleted), whether they
/// are a system admin, how many tenants they are a member of (R13) and when they joined the catalog. The id is the catalog's, not the
/// identity provider's.
/// </summary>
public sealed record UserDetails(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    string Status,
    bool IsSystemAdmin,
    int MembershipCount,
    DateTimeOffset CreatedAt)
{
    // A deletion outranks a suspension: the identity provider no longer knows the person.
    internal static UserDetails Of(User user, bool isSystemAdmin) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, StatusOf(user), isSystemAdmin, user.MembershipCount, user.CreatedAt);

    private static string StatusOf(User user) => (user.DeletedAt, user.SuspendedAt) switch
    {
        ({ }, _) => "Deleted",
        (_, { }) => "Suspended",
        _ => "Active",
    };
}
