using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>The tenant the admin route names, in any status, for a system admin. It runs inside that tenant (W3).</summary>
public sealed record GetTenant;

public static class GetTenantHandler
{
    public static async Task<Result<TenantDetails>> HandleAsync(GetTenant query, ITenantCatalog catalog, CancellationToken cancellationToken) =>
        await catalog.FindTenantAsync(cancellationToken) is { } tenant ? TenantDetails.Of(tenant) : Errors.TenantNotFound;
}
