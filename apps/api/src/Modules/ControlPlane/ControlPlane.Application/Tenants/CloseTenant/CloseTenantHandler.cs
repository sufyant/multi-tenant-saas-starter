using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants.StartTenantPurge;
using ControlPlane.Application.Tenants.WarnTenantPurge;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.CloseTenant;

// The command sets a state (I1): a closed tenant answers as it is, and nothing is announced again. K6h: every change to an invitation
// locks the tenant row first, so under this lock no invitation can be added or accepted: the closure cancels every pending invitation
// in the same transaction (Tenant.Close), and a cancelled one answers as a revoked one (K2); the identity provider's invitations behind
// them are withdrawn after the commit. In the same transaction, through the outbox, it schedules the warning of the tenant's purge and the
// purge's start, each with the closure, which each checks when it arrives (K4). Rejections come first (W7).
public static class CloseTenantHandler
{
    public static async Task<(Result<TenantView>, OutgoingMessages)> HandleAsync(
        CloseTenantCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TenantOptions settings,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        var now = time.GetUtcNow();
        var closed = tenant.Close(now, await tenants.FindPendingInvitationsAsync(cancellationToken));
        if (!closed.IsSuccess || closed.Value.Change == StateChange.Unchanged)
        {
            return (closed.Map(_ => TenantView.Of(tenant)), []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        await tenants.SaveAsync(tenant, cancellationToken);

        return (
            TenantView.Of(tenant),
            [
                new TenantClosedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor, tenant.Name, now + settings.ReopenPeriod),
                .. WithdrawSignUpCommand.For(closed.Value.Cancelled),
                new WarnTenantPurgeCommand(tenant.Id, now).ScheduledAt(now + settings.ReopenPeriod - settings.PurgeWarningLead),
                new StartTenantPurgeCommand(tenant.Id, now).ScheduledAt(now + settings.ReopenPeriod),
            ]);
    }
}
