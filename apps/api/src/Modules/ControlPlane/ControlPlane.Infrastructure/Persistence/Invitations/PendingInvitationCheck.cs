using ControlPlane.Contracts.Invitations;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Invitations;

// W1's third exception: the invitation email asks before each try, and the handler takes no DbContext, so this check opens a short
// read-only transaction of its own. It declares the invitation's tenant (R4), so row level security decides what it sees, and it commits
// before the email service is called. The module's clock decides whether the invitation has expired (Invitation.StateAt). Wolverine
// locates it from the message's scope, so its generated code builds no DbContext for the handler (ControlPlaneInfrastructure).
internal sealed class PendingInvitationCheck(ControlPlaneDbContext controlPlane, TimeProvider time) : IPendingInvitationCheck
{
    public async Task<bool> IsPendingAsync(Guid tenantId, Guid invitationId, CancellationToken cancellationToken)
    {
        await using var transaction = await controlPlane.Database.BeginTransactionAsync(cancellationToken);
        await controlPlane.DeclareTenantAsync(tenantId, cancellationToken);
        var invitation = await controlPlane.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(found => found.Id == invitationId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return invitation?.StateAt(time.GetUtcNow()) == InvitationState.Pending;
    }
}
