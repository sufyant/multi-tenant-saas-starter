using ControlPlane.Application.Users.ListUsers;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// The catalog users for the queries, read without tracking, outside any tenant (R6). It never loads a user to change it;
/// <see cref="IUserRepository"/> does.
/// </summary>
/// <remarks>Public only because Wolverine's generated code passes it to public handlers.</remarks>
public interface IUserQueries
{
    /// <summary>The user with this identity provider id, without a lock, for a read.</summary>
    internal Task<User?> FindUserAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>The user with this catalog id, without a lock, for a read.</summary>
    internal Task<User?> FindUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Whether the user is on the system admin list.</summary>
    internal Task<bool> IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user's memberships in every tenant, by the tenant's name, then id, in one query. It declares the user in the transaction (R11),
    /// so the own_memberships policy decides what is seen; the transaction then declares no tenant (R4). Whether the user is a tenant's
    /// last Owner comes from the tenant's summary (R12), because the policy shows the user's own memberships only.
    /// </summary>
    internal Task<IReadOnlyList<UserMembershipView>> ListMembershipDetailsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// A page of every user in the status or every status, whose email address or name contains the search, with whether each is a
    /// system admin: by the sort field, users without a value for it last, then by id.
    /// </summary>
    internal Task<ListPage<(User User, bool IsSystemAdmin)>> ListUsersAsync(ListRequest<ListUsersSort> list, UserStatus? status, CancellationToken cancellationToken);
}
