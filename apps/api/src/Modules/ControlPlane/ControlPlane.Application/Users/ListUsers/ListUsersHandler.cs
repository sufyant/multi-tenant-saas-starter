using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Users.ListUsers;

public static class ListUsersHandler
{
    public static async Task<Result<UserListView>> HandleAsync(
        ListUsersQuery query,
        IUserQueries users,
        CancellationToken cancellationToken) =>
        await users.ListUsersAsync(
            query.List,
            [.. query.Statuses.Select(Enum.Parse<UserStatus>)],
            query.IsSystemAdmin,
            query.Email is { } email ? EmailAddress.Create(email) : null,
            cancellationToken);
}
