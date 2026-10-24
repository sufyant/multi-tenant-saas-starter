using ControlPlane.Domain.SystemAdmins;
using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Users;

/// <summary>
/// The user aggregate for the commands, loaded to change it and saved. Users belong to no tenant (R6): a tenant command finds its
/// actor here, and a user command declares no tenant; it declares the user only to read the user's own memberships (R11). A method is
/// here when a command needs it to decide.
/// </summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public interface IUserRepository
{
    /// <summary>The id of the user with this identity provider id, without a lock: the system admin who acts.</summary>
    internal Task<Guid?> FindUserIdAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>The profile of the user with this identity provider id, without a lock: the actor of a command.</summary>
    internal Task<UserProfile?> FindProfileAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>
    /// The profile of the user with this id, without a lock: the member whose view a command answers, or the member whose address a
    /// deletion erases from a tenant's invitations.
    /// </summary>
    internal Task<UserProfile?> FindProfileAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The status of the user with this id, without a lock, or none when no user has it: the member the platform makes an Owner.</summary>
    internal Task<UserStatus?> FindStatusAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Whether a user has this id, a deleted user included.</summary>
    internal Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user with this identity provider id, locked for the rest of the transaction, so two events change it one at a time.</summary>
    internal Task<User?> FindUserForUpdateAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>The user with this id, locked for the rest of the transaction, so two changes of their state run one at a time.</summary>
    internal Task<User?> FindUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user with this identity provider id, locked for the rest of the transaction with the lock of an update that keeps the key: a
    /// deletion, which locks the user row for update, runs before or after, never between a check of the user and the change (K6f, A8).
    /// The lock does not block a read, nor an insert of a row that references the user. It first locks the identity provider id, so a
    /// second transaction for a person who is not a user yet waits for the first and then finds the user the first one added (K6f).
    /// </summary>
    internal Task<User?> FindUserForNoKeyUpdateAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>Whether the user is on the system admin list. The system admin list belongs to no tenant.</summary>
    internal Task<bool> IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>When the user's grant on the system admin list was made; null when the user is not on it.</summary>
    internal Task<DateTimeOffset?> FindSystemAdminGrantedAtAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user's grant on the system admin list, locked for the rest of the transaction, to revoke it; none when the user is no system
    /// admin.
    /// </summary>
    internal Task<SystemAdmin?> FindSystemAdminForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    internal void AddSystemAdmin(SystemAdmin systemAdmin);

    internal void RemoveSystemAdmin(SystemAdmin systemAdmin);

    /// <summary>
    /// The user's memberships in every tenant, by tenant id. It declares the user in the transaction (R11), so the own_memberships policy
    /// decides what is seen; the transaction then declares no tenant (R4).
    /// </summary>
    internal Task<IReadOnlyList<UserMembership>> ListMembershipsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The tenants where the user accepted an invitation, by tenant id, also those where no membership is left: the tenants the user
    /// joined (A8). It declares the user in the transaction (R11), so the own_accepted_invitations policy decides what is seen; the
    /// transaction then declares no tenant (R4).
    /// </summary>
    internal Task<IReadOnlyList<Guid>> ListJoinedTenantsAsync(Guid userId, CancellationToken cancellationToken);

    internal void Add(User user);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
