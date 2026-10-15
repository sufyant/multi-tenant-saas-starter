using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>The fields the user list of a system admin sorts by. The first is the default.</summary>
public enum ListUsersSort
{
    Email,
    LastName,
}

/// <summary>
/// Every catalog user, a page at a time, sorted by one field, users without a value for it last, then by id. The search matches the
/// email address, the first name or the last name. The status is one of <see cref="UserDetails.Status"/> (<c>Active</c>,
/// <c>Suspended</c>, <c>Deleted</c>), or null for every status. It runs without a tenant.
/// </summary>
public sealed record ListUsers(ListRequest<ListUsersSort> List, string? Status);

public static class ListUsersHandler
{
    public static async Task<Result<ListPage<UserDetails>>> HandleAsync(
        ListUsers query,
        IUserCatalog users,
        CancellationToken cancellationToken) =>
        (await users.ListUsersAsync(query.List, query.Status, cancellationToken)).Map(found => UserDetails.Of(found.User, found.IsSystemAdmin));
}
