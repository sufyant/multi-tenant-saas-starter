using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Users.ListUsers;

/// <summary>
/// How many users are in each status: the tabs of the Users list. They are the totals of each status, without the search or any filter
/// (the maintainer's decision of 2026-11-10).
/// </summary>
public sealed record UserStatusCountsView(int Active, int Suspended, int Deleted)
{
    internal static UserStatusCountsView Of(IReadOnlyDictionary<UserStatus, int> counts) => new(
        counts.GetValueOrDefault(UserStatus.Active),
        counts.GetValueOrDefault(UserStatus.Suspended),
        counts.GetValueOrDefault(UserStatus.Deleted));
}
