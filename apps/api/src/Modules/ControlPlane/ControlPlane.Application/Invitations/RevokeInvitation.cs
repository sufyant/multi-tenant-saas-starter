using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using SharedKernel;

namespace ControlPlane.Application.Invitations;

/// <summary>An Owner or an Admin revokes a pending invitation of the active tenant; its link stops working at once (K2).</summary>
public sealed record RevokeInvitation(string RevokerId, Guid InvitationId);

// The command sets a state (I1): an invitation already cancelled answers as it is, and nothing is announced again. K6: the tenant row is
// locked first, then the invitation. Row level security hides another tenant's invitation, so its id is not found. Every rejection
// returns before the handler changes anything, because Wolverine commits a failed Result too (W7).
public static class RevokeInvitationHandler
{
    public static async Task<(Result, InvitationRevoked?)> HandleAsync(
        RevokeInvitation command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant door lets a member into an existing tenant only.");
        if (await catalog.FindInvitationForUpdateAsync(command.InvitationId, cancellationToken) is not { } invitation)
        {
            return (Errors.InvitationNotFound, null);
        }

        var revoked = invitation.Revoke();
        if (!revoked.IsSuccess || !revoked.Value)
        {
            return (revoked.IsSuccess ? Result.Success() : Result.Failure(revoked.Error), null);
        }

        var revoker = await catalog.FindUserAsync(command.RevokerId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a catalog user.");
        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (Result.Success(), new InvitationRevoked(Guid.CreateVersion7(now), now, tenant.Id, invitation.Id, revoker.Id));
    }
}
