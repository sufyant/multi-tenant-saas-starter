using ControlPlane.Application.Invitations;
using ControlPlane.Application.Members;
using ControlPlane.Application.Tenants;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Ports;

/// <summary>
/// The one way handlers reach catalog rows that belong to a tenant, used inside the active tenant. Row level security keeps one
/// tenant's memberships and invitations from another. Users are not tenant-owned; they are here because accepting an invitation
/// saves a user and its membership together. The list of every tenant is here too: it is the catalog's own table, which belongs to
/// no tenant, and it is read outside any.
/// </summary>
/// <remarks>
/// Public only because Wolverine's generated code passes it to public handlers; its members speak domain types, so they
/// are internal to the module.
/// </remarks>
public interface ITenantCatalog
{
    internal Guid TenantId { get; }

    /// <summary>
    /// The active tenant, locked for the rest of the transaction, so the steps of its creation, every change of its status or name, and
    /// every change to its memberships and invitations run one at a time (K6). Null when no tenant has the active tenant's id.
    /// </summary>
    internal Task<Tenant?> FindTenantForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>The active tenant, without a lock, for a read. Null when no tenant has the active tenant's id.</summary>
    internal Task<Tenant?> FindTenantAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A page of every tenant whose name or slug contains the search, in the status or every status, by the sort field, then id. It
    /// needs no active tenant.
    /// </summary>
    internal Task<ListPage<(Tenant Tenant, User? FirstOwner)>> ListTenantsAsync(ListRequest<ListTenantsSort> list, TenantStatus? status, CancellationToken cancellationToken);

    /// <summary>
    /// What the active tenant's own rows say for its summary: its members, its Owners, the Owner who joined first, and its latest Owner
    /// invitation. It saves the pending changes first, so it reads the rows as the transaction leaves them. The caller holds the tenant's
    /// lock (K6).
    /// </summary>
    internal Task<TenantRows> ReadRowsAsync(CancellationToken cancellationToken);

    /// <summary>The users who hold the Owner role in the active tenant, in the order they joined, then by user id.</summary>
    internal Task<IReadOnlyList<User>> ListOwnersAsync(CancellationToken cancellationToken);

    internal Task<Membership?> FindMembershipAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// How many memberships of the active tenant hold the Owner role, a deleted user's included. The caller holds the tenant's lock, which
    /// every change to a membership takes first, so the count stays true until the transaction ends (K6).
    /// </summary>
    internal Task<int> CountOwnersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A page of the active tenant's members of the role or every role, with the copy of their profile and whether the user was deleted,
    /// whose email address or name contains the search: by the sort field, then by user id. The search starts from the tenant's
    /// memberships, never from the users.
    /// </summary>
    internal Task<ListPage<(Guid UserId, string? Email, string? FirstName, string? LastName, BuiltInRole Role, bool Deleted, DateTimeOffset JoinedAt)>> ListMembersAsync(
        ListRequest<ListMembersSort> list,
        BuiltInRole? role,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a member of the active tenant has this email address, in the catalog's copy of their profile, compared without case.
    /// The search starts from the tenant's memberships, never from the users.
    /// </summary>
    internal Task<bool> IsMemberEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>How many invitations the active tenant created after the moment, whatever their status (K2).</summary>
    internal Task<int> CountInvitationsSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>
    /// The active tenant's pending invitation to this email address, compared without case; null when it has none. The caller holds the
    /// tenant's lock, which every change to an invitation takes first (K6).
    /// </summary>
    internal Task<Invitation?> FindPendingInvitationAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Every pending invitation of the active tenant, expired ones included. The caller holds the tenant's lock, which every change to an
    /// invitation takes first (K6).
    /// </summary>
    internal Task<IReadOnlyList<Invitation>> FindPendingInvitationsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A page of the active tenant's pending invitations, expired ones included, of the role or every role, whose email address contains
    /// the search, each with the user who invited: by the sort field, then by id.
    /// </summary>
    internal Task<ListPage<(Invitation Invitation, User Inviter)>> ListPendingInvitationsAsync(
        ListRequest<ListInvitationsSort> list,
        BuiltInRole? role,
        CancellationToken cancellationToken);

    /// <summary>The invitation with this token hash, without a lock, for a read; null when the active tenant has none.</summary>
    internal Task<Invitation?> FindInvitationAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>The invitation with this token hash, locked for the rest of the transaction so it is accepted only once.</summary>
    internal Task<Invitation?> FindInvitationForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>The invitation with this id, locked for the rest of the transaction so the steps that change it run one at a time.</summary>
    internal Task<Invitation?> FindInvitationForUpdateAsync(Guid invitationId, CancellationToken cancellationToken);

    internal Task<User?> FindUserAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>
    /// The user with this identity provider id, locked for the rest of the transaction: a deletion, which locks the user row for update,
    /// runs before or after, never between a check of the user and the change (K6, A8). The lock does not block a read, nor an insert of
    /// a row that references the user.
    /// </summary>
    internal Task<User?> FindUserForUpdateAsync(string externalUserId, CancellationToken cancellationToken);

    internal Task<User?> FindUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Whether the user is on the system admin list. The system admin list belongs to no tenant.</summary>
    internal Task<bool> IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the user to the active tenant with the role, and keeps what follows from it (R13): the tenant's summary, and the user's count
    /// of memberships, to which the database adds one, so two tenants' commands for one user never lose a change. Then it saves. The
    /// caller holds the tenant's lock (K6). A user the transaction adds is saved before the count, so it is counted too.
    /// </summary>
    internal Task AddMembershipAsync(Tenant tenant, Guid userId, Guid roleId, DateTimeOffset joinedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the membership from the active tenant, and keeps what follows from it as <see cref="AddMembershipAsync"/> does: the
    /// tenant's summary, and the user's count, from which the database takes one. Then it saves. The caller holds the tenant's lock (K6).
    /// </summary>
    internal Task RemoveMembershipAsync(Tenant tenant, Membership membership, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the active tenant itself, which its creation adds, unless another tenant has its slug. The slug's unique index
    /// decides, also between two tenant creations at the same moment, and nothing else about the other tenant is revealed.
    /// </summary>
    internal Task<bool> TryAddAsync(Tenant tenant, CancellationToken cancellationToken);

    internal void Add(Invitation invitation);

    internal void Add(User user);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
