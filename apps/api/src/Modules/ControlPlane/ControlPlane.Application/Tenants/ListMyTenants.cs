using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>The signed-in user's active and suspended tenants, a page at a time. It runs without a tenant.</summary>
public sealed record ListMyTenants(string UserId, PageRequest Paging);

/// <summary>A tenant as its members see it: active, or suspended so that they cannot enter it.</summary>
public sealed record TenantSummary(Guid Id, string Name, string Slug, string Status);

public static class ListMyTenantsHandler
{
    public static async Task<Result<ListPage<TenantSummary>>> HandleAsync(
        ListMyTenants query,
        IUserTenants tenants,
        CancellationToken cancellationToken) =>
        (await tenants.ListActiveOrSuspendedAsync(query.UserId, query.Paging, cancellationToken))
            .Map(tenant => new TenantSummary(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString()));
}
