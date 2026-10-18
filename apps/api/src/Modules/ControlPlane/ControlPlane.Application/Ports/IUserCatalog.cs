using ControlPlane.Application.Users;
using ControlPlane.Application.Users.UserDeletion;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Ports;

/// <summary>
/// The catalog users, outside any tenant: their names, their deletion, their list and their suspension. Users belong to no tenant (R6),
/// so the transaction declares no tenant; it declares the user only to read the user's own memberships (R11).
/// </summary>
/// <remarks>Public only because Wolverine's generated code passes it to public handlers.</remarks>
public interface IUserCatalog
{
    /// <summary>The user with this identity provider id, locked for the rest of the transaction, so two events change it one at a time.</summary>
    internal Task<User?> FindUserForUpdateAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>The user with this catalog id, locked for the rest of the transaction, so two changes of their state run one at a time.</summary>
    internal Task<User?> FindUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user with this catalog id, without a lock, for a read.</summary>
    internal Task<User?> FindUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user's memberships in every tenant, by tenant id. It declares the user in the transaction (R11), so the own_memberships policy
    /// decides what is seen; the transaction then declares no tenant (R4).
    /// </summary>
    internal Task<IReadOnlyList<UserMembership>> ListMembershipsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user's memberships in every tenant, by the tenant's name, then id, in one query. It declares the user in the transaction (R11),
    /// so the own_memberships policy decides what is seen; the transaction then declares no tenant (R4). Whether the user is a tenant's
    /// last Owner comes from the tenant's summary (R12), because the policy shows the user's own memberships only.
    /// </summary>
    internal Task<IReadOnlyList<UserMembershipDetails>> ListMembershipDetailsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user with this identity provider id, without a lock, for a read.</summary>
    internal Task<User?> FindUserAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>The catalog id of the user with this identity provider id, without a lock: the system admin who acts.</summary>
    internal Task<Guid?> FindUserIdAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>Whether the user is on the system admin list.</summary>
    internal Task<bool> IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// A page of every user in the status or every status, whose email address or name contains the search, with whether each is a
    /// system admin: by the sort field, users without a value for it last, then by id.
    /// </summary>
    internal Task<ListPage<(User User, bool IsSystemAdmin)>> ListUsersAsync(ListRequest<ListUsersSort> list, UserStatus? status, CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
