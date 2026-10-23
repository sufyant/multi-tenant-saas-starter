using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The tenant aggregate for the commands: the active tenant, its memberships and its invitations (M1), loaded to change them and saved.
/// Row level security keeps one tenant's memberships and invitations from another. A method is here when a command needs it to decide.
/// </summary>
/// <remarks>
/// Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.
/// </remarks>
public interface ITenantRepository
{
    internal Guid TenantId { get; }

    /// <summary>
    /// The active tenant, locked for the rest of the transaction, so its creation, every change of its status or name, and
    /// every change to its memberships and invitations run one at a time (K6a). Null when no tenant has the active tenant's id.
    /// </summary>
    internal Task<Tenant?> FindTenantForUpdateAsync(CancellationToken cancellationToken);

    internal Task<Membership?> FindMembershipAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The active tenant's roles with their permissions, read without tracking and without a lock of their own: a command finds a
    /// membership's or an invitation's role, or the role it gives, and a rule asks what the role may do (A1). Every command that writes a
    /// role, its permissions, a membership or an invitation locks the tenant first, so the roles stay as read until the transaction ends
    /// (M1, K6a).
    /// </summary>
    internal Task<TenantRoles> FindRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// What a change to a custom role needs to know of the active tenant (<see cref="RoleFacts"/>): the role with this id and its
    /// permissions, tracked, so the change is saved, or none; how many custom roles the tenant has; whether another of its roles has the
    /// name, compared with <c>lower()</c> as the unique index compares it; and whether a membership or a pending invitation uses the role.
    /// The caller holds the tenant's lock, which every change to a role, a membership or an invitation takes first, so the facts stay true
    /// until the transaction ends (K6a).
    /// </summary>
    internal Task<RoleFacts> FindRoleFactsAsync(Guid? roleId, string? name, CancellationToken cancellationToken);

    /// <summary>Adds the custom role to the active tenant, with its permissions, and saves it. The caller holds the tenant's lock (K6a).</summary>
    internal Task AddRoleAsync(Role role, CancellationToken cancellationToken);

    /// <summary>
    /// Saves what <see cref="Tenant.UpdateRole"/> did to the custom role: its name, the rows of the permissions it added, and the removal
    /// of the rows of the permissions it removed. The caller holds the tenant's lock (K6a).
    /// </summary>
    internal Task SaveRoleAsync(RoleUpdate update, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the custom role of the active tenant and saves it. Its permissions go with it, and a finished invitation that gave it keeps
    /// no role. The caller holds the tenant's lock (K6a).
    /// </summary>
    internal Task DeleteRoleAsync(Role role, CancellationToken cancellationToken);

    /// <summary>
    /// What a change to the user's membership needs to know of the active tenant (<see cref="MemberFacts"/>): the membership, how many
    /// memberships have a role that holds <c>tenant.owners.manage</c>, a deleted user's included, and the tenant's roles. The caller holds the tenant's lock, which every change to a membership
    /// takes first, so the facts stay true until the transaction ends (K6j). The check before a user's deletion reads them without it.
    /// </summary>
    internal Task<MemberFacts> FindMemberFactsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>How many invitations the active tenant created after the moment, whatever their status (K2).</summary>
    internal Task<int> CountInvitationsSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>
    /// What an invitation to this address needs to know of the active tenant (<see cref="Tenant.Invite"/>): how many members have a role
    /// that holds <c>tenant.owners.manage</c>, whether a member
    /// has the address, compared in its one form from the tenant's memberships, how many invitations it created after the moment, and every
    /// pending invitation, expired ones included. The caller holds the tenant's lock, which every change to a membership or an invitation
    /// takes first, so the facts stay true until the transaction ends (K6i, K6j).
    /// </summary>
    internal Task<InvitationFacts> FindInvitationFactsAsync(EmailAddress email, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>
    /// Every pending invitation of the active tenant, expired ones included. The caller holds the tenant's lock, which every change to an
    /// invitation takes first (K6a).
    /// </summary>
    internal Task<IReadOnlyList<Invitation>> FindPendingInvitationsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The active tenant's invitations that the user accepted, or that went to the address or to the address of an invitation the user
    /// accepted, in every state. The caller holds the tenant's lock, which every change to an invitation takes first (K6a).
    /// </summary>
    internal Task<IReadOnlyList<Invitation>> FindInvitationsOfUserAsync(Guid userId, string? email, CancellationToken cancellationToken);

    /// <summary>The invitation with this secret hash, locked for the rest of the transaction so it is accepted only once.</summary>
    internal Task<Invitation?> FindInvitationForUpdateAsync(string secretHash, CancellationToken cancellationToken);

    /// <summary>The invitation with this id, locked for the rest of the transaction so the steps that change it run one at a time.</summary>
    internal Task<Invitation?> FindInvitationForUpdateAsync(Guid invitationId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the transaction's changes, then writes the tenant's summary from its memberships and invitations as the transaction leaves
    /// them (R12). The tenant is the one <see cref="FindTenantForUpdateAsync"/> locked, so the summary is written under the tenant's lock,
    /// in the command's transaction (K6k). Every command that changes a membership or an invitation ends with it: a transaction that
    /// saved such a change and then no summary does not commit.
    /// </summary>
    internal Task SaveAsync(Tenant tenant, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the user to the active tenant with the role, and keeps what follows from it (R13): the tenant's summary, saved as
    /// <see cref="SaveAsync"/> saves it, and the user's count of memberships, to which the database adds one, so two tenants' commands
    /// for one user never lose a change. The caller holds the tenant's lock (K6k). A user the transaction adds is saved before the count,
    /// so it is counted too.
    /// </summary>
    internal Task AddMembershipAsync(Tenant tenant, Guid userId, Guid roleId, DateTimeOffset joinedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the membership from the active tenant, and keeps what follows from it as <see cref="AddMembershipAsync"/> does: the
    /// tenant's summary, and the user's count, from which the database takes one. The caller holds the tenant's lock (K6k).
    /// </summary>
    internal Task RemoveMembershipAsync(Tenant tenant, Membership membership, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the active tenant itself, which its creation adds, with its summary (R12) and its built-in roles (A2), unless another tenant
    /// has its slug. The slug's unique index decides, also between two tenant creations at the same moment, and nothing else about the
    /// other tenant is revealed. The roles are saved at once, so the creation's next steps find them.
    /// </summary>
    internal Task<bool> TryAddAsync(Tenant tenant, IReadOnlyList<Role> roles, CancellationToken cancellationToken);

    /// <summary>
    /// Saves what <see cref="Tenant.Invite"/> did, as <see cref="SaveAsync"/> saves it: the replaced invitations first, so the partial
    /// unique index never sees two pending invitations to one address (K2), then the new one, with the tenant's summary.
    /// </summary>
    internal Task AddInvitationAsync(Tenant tenant, Invited invited, CancellationToken cancellationToken);

    /// <summary>
    /// The Owners of the active tenant, the members whose role holds <c>tenant.owners.manage</c> (A1), who have an address: a deleted user
    /// has none (A8). Each with the language the user keeps. Read under the tenant's lock, as values.
    /// </summary>
    internal Task<IReadOnlyList<OwnerAddress>> FindOwnerAddressesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes ControlPlane's data of the active tenant for its purge, in the order the keys need, and saves the tenant, which
    /// <see cref="Tenant.Purge"/> made its tombstone (docs/flows/tenant-purge.md): the invitations, then the memberships, each with the
    /// user's count of memberships (R13), then the roles with their permissions, then the tenant's summary (R12). The caller holds the
    /// tenant's lock (K6a); the counts are changed one user at a time, in the order of the user ids. Answers the former members whom the
    /// purge left without a membership, who are neither deleted nor system admins: the users left without a tenant.
    /// </summary>
    internal Task<IReadOnlyList<Guid>> PurgeAsync(Tenant tenant, CancellationToken cancellationToken);

    /// <summary>Saves the transaction's changes without the summary: a change to the tenant itself, or a step before <see cref="SaveAsync"/>.</summary>
    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
