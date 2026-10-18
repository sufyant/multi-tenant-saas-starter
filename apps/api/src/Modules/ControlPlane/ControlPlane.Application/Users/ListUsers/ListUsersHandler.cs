using ControlPlane.Application.Ports;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Users.ListUsers;

public static class ListUsersHandler
{
    public static async Task<Result<ListPage<UserView>>> HandleAsync(
        ListUsersQuery query,
        IUserCatalog users,
        CancellationToken cancellationToken) =>
        (await users.ListUsersAsync(query.List, query.Status is { } status ? Enum.Parse<UserStatus>(status) : null, cancellationToken)).Map(found => UserView.Of(found.User, found.IsSystemAdmin));
}
