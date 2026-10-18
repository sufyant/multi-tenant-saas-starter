using ControlPlane.Application.Ports;
using ControlPlane.Contracts.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants.ReactivateTenant;

// The command sets a state (I1): an active tenant answers as it is, and nothing is announced. Every rejection returns before the
// handler changes anything, because Wolverine commits a failed Result too (W7).
public static class ReactivateTenantHandler
{
    public static async Task<(Result<TenantView>, TenantReactivatedEvent?)> HandleAsync(
        ReactivateTenantCommand command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null);
        }

        var reactivated = tenant.Reactivate();
        if (!reactivated.IsSuccess || reactivated.Value == StateChange.Unchanged)
        {
            return (reactivated.Map(_ => TenantView.Of(tenant)), null);
        }

        var actor = await catalog.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");
        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (TenantView.Of(tenant), new TenantReactivatedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor.Id, tenant.Name));
    }
}
