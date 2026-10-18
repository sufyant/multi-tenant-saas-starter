using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The tenant aggregate for the commands: the active tenant, its memberships and its invitations (M1), loaded to change them and saved.
/// Row level security keeps one tenant's memberships and invitations from another. A method is here when a command needs it to decide.
/// </summary>
/// <remarks>
/// Public (W9); its members speak domain types, so they are internal to the module.
/// </remarks>
public interface ITenantRepository
{
    internal Guid TenantId { get; }

    /// <summary>
    /// The active tenant, locked for the rest of the transaction, so the steps of its creation, every change of its status or name, and
    /// every change to its memberships and invitations run one at a time (K6a). Null when no tenant has the active tenant's id.
    /// </summary>
    internal Task<Tenant?> FindTenantForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The active tenant's summary: its members, its Owners, the Owner who joined first, and its latest Owner invitation. It saves the
    /// pending changes first, so it reads the memberships and invitations as the transaction leaves them. The caller holds the tenant's
    /// lock (K6k).
    /// </summary>
    internal Task<TenantSummary> ReadSummaryAsync(CancellationToken cancellationToken);

    internal Task<Membership?> FindMembershipAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// How many memberships of the active tenant hold the Owner role, a deleted user's included. The caller holds the tenant's lock, which
    /// every change to a membership takes first, so the count stays true until the transaction ends (K6j).
    /// </summary>
    internal Task<int> CountOwnersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether a member of the active tenant has this email address, in our copy of their profile, compared without case.
    /// The search starts from the tenant's memberships, never from the users.
    /// </summary>
    internal Task<bool> IsMemberEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>How many invitations the active tenant created after the moment, whatever their status (K2).</summary>
    internal Task<int> CountInvitationsSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>
    /// The active tenant's pending invitation to this email address, compared without case; null when it has none. The caller holds the
    /// tenant's lock, which every change to an invitation takes first (K6a).
    /// </summary>
    internal Task<Invitation?> FindPendingInvitationAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Every pending invitation of the active tenant, expired ones included. The caller holds the tenant's lock, which every change to an
    /// invitation takes first (K6a).
    /// </summary>
    internal Task<IReadOnlyList<Invitation>> FindPendingInvitationsAsync(CancellationToken cancellationToken);

    /// <summary>The invitation with this secret hash, locked for the rest of the transaction so it is accepted only once.</summary>
    internal Task<Invitation?> FindInvitationForUpdateAsync(string secretHash, CancellationToken cancellationToken);

    /// <summary>The invitation with this id, locked for the rest of the transaction so the steps that change it run one at a time.</summary>
    internal Task<Invitation?> FindInvitationForUpdateAsync(Guid invitationId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the user to the active tenant with the role, and keeps what follows from it (R13): the tenant's summary, and the user's count
    /// of memberships, to which the database adds one, so two tenants' commands for one user never lose a change. Then it saves. The
    /// caller holds the tenant's lock (K6k). A user the transaction adds is saved before the count, so it is counted too.
    /// </summary>
    internal Task AddMembershipAsync(Tenant tenant, Guid userId, Guid roleId, DateTimeOffset joinedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the membership from the active tenant, and keeps what follows from it as <see cref="AddMembershipAsync"/> does: the
    /// tenant's summary, and the user's count, from which the database takes one. Then it saves. The caller holds the tenant's lock (K6k).
    /// </summary>
    internal Task RemoveMembershipAsync(Tenant tenant, Membership membership, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the active tenant itself, which its creation adds, unless another tenant has its slug. The slug's unique index
    /// decides, also between two tenant creations at the same moment, and nothing else about the other tenant is revealed.
    /// </summary>
    internal Task<bool> TryAddAsync(Tenant tenant, CancellationToken cancellationToken);

    internal void Add(Invitation invitation);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
