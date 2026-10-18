using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A system admin gives a suspended tenant back to its members. It runs inside that tenant, which the platform route names, with the
/// tenant locked, so a suspension and a reactivation run one at a time.
/// </summary>
public sealed record ReactivateTenantCommand(string AdminId);

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
        if (!reactivated.IsSuccess || !reactivated.Value)
        {
            return (reactivated.Map(_ => TenantView.Of(tenant)), null);
        }

        var admin = await catalog.FindUserAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");
        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (TenantView.Of(tenant), new TenantReactivatedEvent(Guid.CreateVersion7(now), now, tenant.Id, admin.Id, tenant.Name));
    }
}
