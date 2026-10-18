using ControlPlane.Application.Ports;
using ControlPlane.Contracts.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants.RenameTenant;

// The command sets a state (I1): the name the tenant has answers as it is, and nothing is announced. Every rejection returns before the
// handler changes anything, because Wolverine commits a failed Result too (W7).
public static class RenameTenantHandler
{
    public static async Task<(Result<TenantView>, TenantRenamedEvent?)> HandleAsync(
        RenameTenantCommand command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null);
        }

        var oldName = tenant.Name;
        var renamed = tenant.Rename(command.Name);
        if (!renamed.IsSuccess || renamed.Value == StateChange.Unchanged)
        {
            return (renamed.Map(_ => TenantView.Of(tenant)), null);
        }

        var actor = await catalog.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");
        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (TenantView.Of(tenant), new TenantRenamedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor.Id, oldName, tenant.Name));
    }
}
