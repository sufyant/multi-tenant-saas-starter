using ControlPlane.Application.Invitations.ListInvitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// The active tenant's invitations for the queries, read without tracking. Row level security keeps them to the tenant. It never loads
/// an invitation to change it; <see cref="Tenants.ITenantRepository"/> does, because an invitation belongs to the tenant aggregate (M1).
/// </summary>
/// <remarks>Public only because Wolverine's generated code passes it to public handlers.</remarks>
public interface IInvitationQueries
{
    /// <summary>
    /// A page of the active tenant's pending invitations, expired ones included, of the role or every role, whose email address contains
    /// the search, each with the user who invited: by the sort field, then by id.
    /// </summary>
    internal Task<ListPage<(Invitation Invitation, User Inviter)>> ListPendingInvitationsAsync(
        ListRequest<ListInvitationsSort> list,
        BuiltInRole? role,
        CancellationToken cancellationToken);

    /// <summary>The invitation with this secret hash, without a lock, for a read; null when the active tenant has none.</summary>
    internal Task<Invitation?> FindInvitationAsync(string secretHash, CancellationToken cancellationToken);
}
