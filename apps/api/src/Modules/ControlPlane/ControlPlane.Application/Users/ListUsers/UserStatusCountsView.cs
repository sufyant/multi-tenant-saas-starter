using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Users.ListUsers;

/// <summary>
/// How many users of the list are in each status: the tabs of the Users list. They follow the search and every other filter, and ignore
/// the status filter, as the Open and Closed counts of GitHub Issues do (the maintainer's decision of 2026-11-08).
/// </summary>
public sealed record UserStatusCountsView(int Active, int Suspended, int Deleted)
{
    internal static UserStatusCountsView Of(IReadOnlyDictionary<UserStatus, int> counts) => new(
        counts.GetValueOrDefault(UserStatus.Active),
        counts.GetValueOrDefault(UserStatus.Suspended),
        counts.GetValueOrDefault(UserStatus.Deleted));
}
