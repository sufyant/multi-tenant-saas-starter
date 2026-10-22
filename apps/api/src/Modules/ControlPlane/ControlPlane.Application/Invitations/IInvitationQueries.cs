using ControlPlane.Application.Invitations.ListInvitations;
using SharedKernel;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// The active tenant's invitations for the queries: each method reads without tracking and answers a read model, built in the query (Q2).
/// Row level security keeps them to the tenant. It never loads an invitation to change it; <see cref="Tenants.ITenantRepository"/> does,
/// because an invitation belongs to the tenant aggregate (M1).
/// </summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public interface IInvitationQueries
{
    /// <summary>
    /// A page of the active tenant's pending invitations, expired ones included, of the role with this id or every role, whose email address contains
    /// the search, each with the user who invited and its state at the moment: by the sort field, then by id.
    /// </summary>
    internal Task<ListPage<InvitationView>> ListPendingInvitationsAsync(
        ListRequest<ListInvitationsSort> list,
        Guid? roleId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>The invitation with this secret hash, with the user who invited and its state at the moment; null when the active tenant has none.</summary>
    internal Task<InvitationView?> FindInvitationAsync(string secretHash, DateTimeOffset now, CancellationToken cancellationToken);
}
