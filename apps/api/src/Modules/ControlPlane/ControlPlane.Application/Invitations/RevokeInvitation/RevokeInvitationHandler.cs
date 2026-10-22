using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Invitations;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Invitations.RevokeInvitation;

// The command sets a state (I1): an invitation already cancelled answers as it is, and nothing is announced again. K6: the tenant row
// is locked first; a revoker who is no longer a member finds no tenant (T3), and a tenant that is no longer active is refused; then the
// invitation is locked. Row level security hides another tenant's invitation, so its id is not found. The rules are the tenant's (M1,
// Tenant.Revoke), the escalation rule among them: the revoker's role holds every permission of the invitation's role.
// Rejections come first (W7).
public static class RevokeInvitationHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        RevokeInvitationCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var start = await TenantActor.LockAsync(tenants, users, command.ActorId, cancellationToken);
        if (!start.IsSuccess)
        {
            return (start.Error, []);
        }

        var (tenant, actor, actorMembership) = start.Value;

        var invitation = await tenants.FindInvitationForUpdateAsync(command.InvitationId, cancellationToken);
        var roles = await tenants.FindRolesAsync(cancellationToken);
        var revoked = tenant.Revoke(roles.Of(actorMembership.RoleId), invitation, roles);
        if (!revoked.IsSuccess || revoked.Value == StateChange.Unchanged)
        {
            return (revoked.IsSuccess ? Result.Success() : Result.Failure(revoked.Error), []);
        }

        await tenants.SaveAsync(tenant, cancellationToken);

        var now = time.GetUtcNow();
        return (
            Result.Success(),
            [new InvitationRevokedEvent(Guid.CreateVersion7(now), now, tenant.Id, invitation!.Id, actor.Id), .. WithdrawSignUpCommand.For([invitation])]);
    }
}
