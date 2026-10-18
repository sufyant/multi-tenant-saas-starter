using SharedKernel;

namespace ControlPlane.Application.Tenants.ListMyTenants;

public static class ListMyTenantsHandler
{
    public static async Task<Result<ListPage<MyTenantView>>> HandleAsync(
        ListMyTenantsQuery query,
        ITenantQueries tenants,
        CancellationToken cancellationToken) =>
        await tenants.ListMyTenantsAsync(query.ActorId, query.List, cancellationToken);
}
