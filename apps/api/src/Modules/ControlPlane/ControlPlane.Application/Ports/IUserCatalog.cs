using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Ports;

/// <summary>
/// The catalog users, outside any tenant: the copy of their profile, their deletion, their list and their suspension. Users belong to
/// no tenant (R6), so the transaction declares neither a tenant nor a user.
/// </summary>
/// <remarks>Public only because Wolverine's generated code passes it to public handlers.</remarks>
public interface IUserCatalog
{
    /// <summary>The user with this identity provider id, locked for the rest of the transaction, so two events change it one at a time.</summary>
    internal Task<User?> FindUserForUpdateAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>The user with this catalog id, locked for the rest of the transaction, so two changes of their state run one at a time.</summary>
    internal Task<User?> FindUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The catalog id of the user with this identity provider id, without a lock: the system admin who acts.</summary>
    internal Task<Guid?> FindUserIdAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>Whether the user is on the staff list.</summary>
    internal Task<bool> IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// A page of every user, with whether each is a system admin: by email address, users without one last, then by id.
    /// </summary>
    internal Task<ListPage<(User User, bool IsSystemAdmin)>> ListUsersAsync(PageRequest paging, CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
