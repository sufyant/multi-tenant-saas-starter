using ControlPlane.Application.Invitations;
using ControlPlane.Application.Invitations.ListInvitations;
using ControlPlane.Application.Users;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Infrastructure.Persistence.Lists;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.Invitations;

/// <summary>
/// The active tenant's invitations for the queries, read without tracking. Row level security keeps them to the tenant; the queries add
/// no filter of their own.
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class InvitationQueries(ControlPlaneDbContext controlPlane) : IInvitationQueries
{
    // Row level security keeps the list to the active tenant; the query names no tenant. Two pending invitations never share an address,
    // so only the expiry can tie, and the id breaks every tie (K5). The users table is not under row level security (R6): the page joins
    // each inviter's user in the same query, so their names are read with the page, with no query for each invitation.
    async Task<ListPage<InvitationView>> IInvitationQueries.ListPendingInvitationsAsync(
        ListRequest<ListInvitationsSort> list,
        BuiltInRole? role,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitations = controlPlane.TenantInvitations.Where(invitation => invitation.Status == InvitationStatus.Pending);
        if (role is { } wanted)
        {
            var roleId = BuiltInRoles.All.Single(builtIn => builtIn.BuiltIn == wanted).Id;
            invitations = invitations.Where(invitation => invitation.RoleId == roleId);
        }

        if (list.Search is { } search)
        {
            var pattern = ListQueryExtensions.ContainsPattern(search);
            invitations = invitations.Where(invitation => EF.Functions.ILike(invitation.Email, pattern, ListQueryExtensions.Escape));
        }

        var total = await invitations.CountAsync(cancellationToken);
        var sorted = list.Sort switch
        {
            ListInvitationsSort.Email => invitations.OrderBy(invitation => invitation.Email, list.Descending),
            _ => invitations.OrderBy(invitation => invitation.ExpiresAt, list.Descending),
        };
        var page = await sorted
            .ThenBy(invitation => invitation.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .Join(controlPlane.Users, invitation => invitation.InvitedBy, user => user.Id, (invitation, user) => new { Invitation = invitation, Inviter = user })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ListPage.Of([.. page.Select(row => InvitationView.Of(row.Invitation, UserNameView.Of(row.Inviter), now))], list.Paging, total);
    }

    // Row level security keeps the invitations to the active tenant, so the secret of another tenant's invitation finds none. The users
    // table is not under row level security (R6): one query joins the inviter's user, as the list does.
    async Task<InvitationView?> IInvitationQueries.FindInvitationAsync(string secretHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var found = await controlPlane.TenantInvitations
            .Where(invitation => invitation.SecretHash == secretHash)
            .Join(controlPlane.Users, invitation => invitation.InvitedBy, user => user.Id, (invitation, user) => new { Invitation = invitation, Inviter = user })
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return found is null ? null : InvitationView.Of(found.Invitation, UserNameView.Of(found.Inviter), now);
    }
}
