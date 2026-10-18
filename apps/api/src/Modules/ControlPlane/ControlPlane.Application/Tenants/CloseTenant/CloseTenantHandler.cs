using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.CloseTenant;

// The command sets a state (I1): a closed tenant answers as it is, and nothing is announced again. K6h: every change to an invitation
// locks the tenant row first, so under this lock no invitation can be added or accepted: the closure cancels every pending invitation
// in the same transaction, and a cancelled one answers as a revoked one (K2); the identity provider's invitations behind them are
// withdrawn after the commit. Rejections come first (W7).
public static class CloseTenantHandler
{
    public static async Task<(Result<TenantView>, TenantClosedEvent?, OutgoingMessages)> HandleAsync(
        CloseTenantCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null, []);
        }

        var now = time.GetUtcNow();
        var closed = tenant.Close(now);
        if (!closed.IsSuccess || closed.Value == StateChange.Unchanged)
        {
            return (closed.Map(_ => TenantView.Of(tenant)), null, []);
        }

        var withdrawals = new OutgoingMessages();
        foreach (var invitation in await tenants.FindPendingInvitationsAsync(cancellationToken))
        {
            invitation.Cancel();
            if (invitation.SignUpInvitationId is { } signUp)
            {
                withdrawals.Add(new WithdrawSignUpCommand(signUp));
            }
        }

        var actor = await users.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        tenant.Summarize(await tenants.ReadSummaryAsync(cancellationToken));
        await tenants.SaveChangesAsync(cancellationToken);

        return (TenantView.Of(tenant), new TenantClosedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor.Id, tenant.Name), withdrawals);
    }
}
