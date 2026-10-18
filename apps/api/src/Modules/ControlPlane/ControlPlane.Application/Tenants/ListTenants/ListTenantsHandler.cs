using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants.ListTenants;

public static class ListTenantsHandler
{
    public static async Task<Result<ListPage<TenantListItemView>>> HandleAsync(
        ListTenantsQuery query,
        ITenantQueries tenants,
        TenantOptions settings,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        await tenants.ListTenantsAsync(
            query.List,
            query.Status is { } status ? Enum.Parse<TenantStatus>(status) : null,
            settings.ReopenPeriod,
            time.GetUtcNow(),
            cancellationToken);
}
