using ControlPlane.Application.Invitations.SignUp;
using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// An Owner or an Admin revokes a pending invitation of the active tenant; its link stops working at once (K2), and the identity provider's
/// invitation behind it is withdrawn after the commit.
/// </summary>
public sealed record RevokeInvitationCommand(string ActorId, Guid InvitationId);

// The command sets a state (I1): an invitation already cancelled answers as it is, and nothing is announced again. K6: the tenant row is
// locked first; a revoker who is no longer a member finds no tenant (T3), and a tenant that is no longer active is refused; then the
// invitation is locked. Row level security hides another tenant's invitation, so its id is not found. Every rejection returns before
// the handler changes anything, because Wolverine commits a failed Result too (W7).
public static class RevokeInvitationHandler
{
    public static async Task<(Result, InvitationRevokedEvent?, WithdrawSignUpCommand?)> HandleAsync(
        RevokeInvitationCommand command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant door lets a member into an existing tenant only.");
        var revoker = await catalog.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a catalog user.");

        // The door let the revoker in, but a change that held the lock before may have removed them: to a non-member the tenant does not
        // exist (T3).
        if (await catalog.FindMembershipAsync(revoker.Id, cancellationToken) is null)
        {
            return (Errors.TenantNotFound, null, null);
        }

        // The door let the revoker into an active tenant, but a system admin may have suspended or closed it while this request waited for
        // the lock: under the lock, a tenant that is no longer active is refused (K6).
        var access = Tenant.CheckMemberAccess(tenant.Status);
        if (!access.IsSuccess)
        {
            return (access, null, null);
        }

        if (await catalog.FindInvitationForUpdateAsync(command.InvitationId, cancellationToken) is not { } invitation)
        {
            return (Errors.InvitationNotFound, null, null);
        }

        var revoked = invitation.Revoke();
        if (!revoked.IsSuccess || revoked.Value == StateChange.Unchanged)
        {
            return (revoked.IsSuccess ? Result.Success() : Result.Failure(revoked.Error), null, null);
        }

        tenant.Summarize(await catalog.ReadRowsAsync(cancellationToken));
        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (
            Result.Success(),
            new InvitationRevokedEvent(Guid.CreateVersion7(now), now, tenant.Id, invitation.Id, revoker.Id),
            invitation.SignUpInvitationId is { } signUp ? new WithdrawSignUpCommand(signUp) : null);
    }
}
