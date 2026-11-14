using ControlPlane.Contracts.Invitations;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Invitations;

// The addresses of a page of invitations in one query. The invitations are a tenant's rows (R1): the caller's transaction declares the
// tenant, and row level security hides every other tenant's. Public because Wolverine's generated code builds it on this module's DbContext
// (W1, W9).
public sealed class InvitationDirectory(ControlPlaneDbContext controlPlane) : IInvitationDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> FindEmailsAsync(IReadOnlyCollection<Guid> invitationIds, CancellationToken cancellationToken)
    {
        var invitations = await controlPlane.Invitations
            .Where(invitation => invitationIds.Contains(invitation.Id) && invitation.Email != null)
            .Select(invitation => new { invitation.Id, invitation.Email })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return invitations.ToDictionary(invitation => invitation.Id, invitation => invitation.Email!);
    }
}
