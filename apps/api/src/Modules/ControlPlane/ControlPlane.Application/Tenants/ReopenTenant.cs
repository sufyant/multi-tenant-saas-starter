using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A system admin gives a closed tenant back to its members, while its reopen period runs (K4). It runs inside that tenant, which the
/// platform route names, with the tenant locked. The invitations the closure cancelled stay cancelled.
/// </summary>
public sealed record ReopenTenantCommand(string ActorId);

// The command sets a state (I1): an active tenant answers as it is, and nothing is announced. Every rejection returns before the handler
// changes anything, because Wolverine commits a failed Result too (W7).
public static class ReopenTenantHandler
{
    public static async Task<(Result<TenantView>, TenantReopenedEvent?)> HandleAsync(
        ReopenTenantCommand command,
        ITenantCatalog catalog,
        TenantOptions settings,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null);
        }

        var now = time.GetUtcNow();
        var reopened = tenant.Reopen(now, settings.ReopenPeriod);
        if (!reopened.IsSuccess || reopened.Value == StateChange.Unchanged)
        {
            return (reopened.Map(_ => TenantView.Of(tenant)), null);
        }

        var actor = await catalog.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");
        await catalog.SaveChangesAsync(cancellationToken);

        return (TenantView.Of(tenant), new TenantReopenedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor.Id, tenant.Name));
    }
}
