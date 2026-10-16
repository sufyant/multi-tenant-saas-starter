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
    /// The active tenant, locked for the rest of the transaction, so the steps of its creation, its suspension and reactivation, and
    /// every change to its memberships and invitations run one at a time (K6). Null when no tenant has the active tenant's id.
    /// </summary>
    internal Task<Tenant?> FindTenantForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>The active tenant, without a lock, for a read. Null when no tenant has the active tenant's id.</summary>
    internal Task<Tenant?> FindTenantAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A page of every tenant whose name or slug contains the search, in the status or every status, by the sort field, then id. It
    /// needs no active tenant.
    /// </summary>
    internal Task<ListPage<Tenant>> ListTenantsAsync(ListRequest<ListTenantsSort> list, TenantStatus? status, CancellationToken cancellationToken);

    internal Task<Membership?> FindMembershipAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// A page of the active tenant's members of the role or every role, with the copy of their profile, whose email address or name
    /// contains the search: by the sort field, then by user id. The search starts from the tenant's memberships, never from the users.
    /// </summary>
    internal Task<ListPage<(Guid UserId, string? Email, string? FirstName, string? LastName, BuiltInRole Role)>> ListMembersAsync(
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
    /// A page of the active tenant's pending invitations, expired ones included, of the role or every role, whose email address contains
    /// the search: by the sort field, then by id.
    /// </summary>
    internal Task<ListPage<Invitation>> ListPendingInvitationsAsync(
        ListRequest<ListInvitationsSort> list,
        BuiltInRole? role,
        CancellationToken cancellationToken);

    /// <summary>The invitation with this token hash, locked for the rest of the transaction so it is accepted only once.</summary>
    internal Task<Invitation?> FindInvitationForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>The invitation with this id, locked for the rest of the transaction so the steps that change it run one at a time.</summary>
    internal Task<Invitation?> FindInvitationForUpdateAsync(Guid invitationId, CancellationToken cancellationToken);

    internal Task<User?> FindUserAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>Whether the user is on the staff list. The staff list belongs to no tenant.</summary>
    internal Task<bool> IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken);

    internal void AddMember(Guid userId, Guid roleId);

    /// <summary>
    /// Adds the active tenant itself, which its creation adds, unless another tenant has its slug. The slug's unique index
    /// decides, also between two tenant creations at the same moment, and nothing else about the other tenant is revealed.
    /// </summary>
    internal Task<bool> TryAddAsync(Tenant tenant, CancellationToken cancellationToken);

    internal void Add(Invitation invitation);

    internal void Add(User user);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
