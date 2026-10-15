using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>Every catalog user, a page at a time: by email address, users without one last, then by id. It runs without a tenant.</summary>
public sealed record ListUsers(PageRequest Paging);

public static class ListUsersHandler
{
    public static async Task<Result<ListPage<UserDetails>>> HandleAsync(
        ListUsers query,
        IUserCatalog users,
        CancellationToken cancellationToken) =>
        (await users.ListUsersAsync(query.Paging, cancellationToken)).Map(found => UserDetails.Of(found.User, found.IsSystemAdmin));
}
