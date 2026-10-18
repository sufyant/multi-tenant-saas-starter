using ControlPlane.Application.Invitations.SignUp;
using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A system admin closes an active or a suspended tenant: its members cannot enter, its data stays, and it can be reopened within the
/// reopen period (K4). It runs inside that tenant, which the platform route names, with the tenant locked.
/// </summary>
public sealed record CloseTenantCommand(string ActorId);

// The command sets a state (I1): a closed tenant answers as it is, and nothing is announced again. K6: every change to an invitation
// locks the tenant row first, so under this lock no invitation can be added or accepted: the closure cancels every pending invitation
// in the same transaction, and a cancelled one answers as a revoked one (K2); the identity provider's invitations behind them are
// withdrawn after the commit. Every rejection returns before the handler changes
// anything, because Wolverine commits a failed Result too (W7).
public static class CloseTenantHandler
{
    public static async Task<(Result<TenantView>, TenantClosedEvent?, OutgoingMessages)> HandleAsync(
        CloseTenantCommand command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null, []);
        }

        var now = time.GetUtcNow();
        var closed = tenant.Close(now);
        if (!closed.IsSuccess || !closed.Value)
        {
            return (closed.Map(_ => TenantView.Of(tenant)), null, []);
        }

        var withdrawals = new OutgoingMessages();
        foreach (var invitation in await catalog.FindPendingInvitationsAsync(cancellationToken))
        {
            invitation.Cancel();
            if (invitation.SignUpInvitationId is { } signUp)
            {
                withdrawals.Add(new WithdrawSignUpCommand(signUp));
            }
        }

        var actor = await catalog.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");
        tenant.Summarize(await catalog.ReadRowsAsync(cancellationToken));
        await catalog.SaveChangesAsync(cancellationToken);

        return (TenantView.Of(tenant), new TenantClosedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor.Id, tenant.Name), withdrawals);
    }
}
