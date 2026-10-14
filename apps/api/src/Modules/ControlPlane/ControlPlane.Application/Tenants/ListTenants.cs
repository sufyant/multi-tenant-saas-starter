using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>Every tenant, in every status, a page at a time: by name, then id. It runs without a tenant.</summary>
public sealed record ListTenants(PageRequest Paging);

public static class ListTenantsHandler
{
    public static async Task<Result<ListPage<TenantDetails>>> HandleAsync(
        ListTenants query,
        ITenantCatalog catalog,
        CancellationToken cancellationToken) =>
        (await catalog.ListTenantsAsync(query.Paging, cancellationToken)).Map(TenantDetails.Of);
}
