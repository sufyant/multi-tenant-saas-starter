using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>The active tenant, for one of its members. The tenant door has let the member in, so the tenant is active.</summary>
public sealed record GetMyTenant;

public static class GetMyTenantHandler
{
    public static async Task<Result<TenantSummary>> HandleAsync(GetMyTenant query, ITenantCatalog catalog, CancellationToken cancellationToken) =>
        await catalog.FindTenantAsync(cancellationToken) is { } tenant
            ? new TenantSummary(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString())
            : Errors.TenantNotFound;
}
