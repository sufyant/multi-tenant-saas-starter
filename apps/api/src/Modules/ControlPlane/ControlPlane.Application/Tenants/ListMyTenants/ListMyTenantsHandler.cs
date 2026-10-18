using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Tenants.ListMyTenants;

public static class ListMyTenantsHandler
{
    public static async Task<Result<ListPage<MyTenantView>>> HandleAsync(
        ListMyTenantsQuery query,
        IUserTenants tenants,
        CancellationToken cancellationToken) =>
        (await tenants.ListPastCreationAsync(query.ActorId, query.List, cancellationToken))
            .Map(found => MyTenantView.Of(found.Tenant, found.Role));
}
