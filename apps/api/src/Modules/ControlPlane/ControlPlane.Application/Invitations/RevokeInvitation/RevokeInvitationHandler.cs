using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Invitations;
using SharedKernel;

namespace ControlPlane.Application.Invitations.RevokeInvitation;

// The command sets a state (I1): an invitation already cancelled answers as it is, and nothing is announced again. K6: the tenant row
// is locked first; a revoker who is no longer a member finds no tenant (T3), and a tenant that is no longer active is refused; then the
// invitation is locked. Row level security hides another tenant's invitation, so its id is not found. The rule is the tenant's (M1, Tenant.Revoke).
// Rejections come first (W7).
public static class RevokeInvitationHandler
{
    public static async Task<(Result, InvitationRevokedEvent?, WithdrawSignUpCommand?)> HandleAsync(
        RevokeInvitationCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var start = await TenantActor.LockAsync(tenants, users, command.ActorId, cancellationToken);
        if (!start.IsSuccess)
        {
            return (start.Error, null, null);
        }

        var (tenant, actor, _) = start.Value;

        var invitation = await tenants.FindInvitationForUpdateAsync(command.InvitationId, cancellationToken);
        var revoked = tenant.Revoke(invitation);
        if (!revoked.IsSuccess || revoked.Value == StateChange.Unchanged)
        {
            return (revoked.IsSuccess ? Result.Success() : Result.Failure(revoked.Error), null, null);
        }

        await tenants.SaveAsync(tenant, cancellationToken);

        var now = time.GetUtcNow();
        return (
            Result.Success(),
            new InvitationRevokedEvent(Guid.CreateVersion7(now), now, tenant.Id, invitation!.Id, actor.Id),
            WithdrawSignUpCommand.For([invitation]).SingleOrDefault());
    }
}
