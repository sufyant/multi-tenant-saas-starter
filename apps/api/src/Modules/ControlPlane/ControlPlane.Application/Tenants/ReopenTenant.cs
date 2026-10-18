using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A system admin gives a closed tenant back to its members, while its reopen period runs (K4). It runs inside that tenant, which the
/// platform route names, with the tenant locked. The invitations the closure cancelled stay cancelled.
/// </summary>
public sealed record ReopenTenant(string AdminId);

// The command sets a state (I1): an active tenant answers as it is, and nothing is announced. Every rejection returns before the handler
// changes anything, because Wolverine commits a failed Result too (W7).
public static class ReopenTenantHandler
{
    public static async Task<(Result<TenantDetails>, TenantReopened?)> HandleAsync(
        ReopenTenant command,
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
        if (!reopened.IsSuccess || !reopened.Value)
        {
            return (reopened.Map(_ => TenantDetails.Of(tenant)), null);
        }

        var admin = await catalog.FindUserAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");
        await catalog.SaveChangesAsync(cancellationToken);

        return (TenantDetails.Of(tenant), new TenantReopened(Guid.CreateVersion7(now), now, tenant.Id, admin.Id, tenant.Name));
    }
}
