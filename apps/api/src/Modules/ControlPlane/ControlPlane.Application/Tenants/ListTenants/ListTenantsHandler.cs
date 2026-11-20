using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants.ListTenants;

public static class ListTenantsHandler
{
    public static async Task<Result<TenantListView>> HandleAsync(
        ListTenantsQuery query,
        ITenantQueries tenants,
        TenantOptions settings,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        await tenants.ListTenantsAsync(
            query.List,
            [.. query.Statuses.Select(Enum.Parse<TenantStatus>)],
            query.Only,
            settings.ReopenPeriod,
            time.GetUtcNow(),
            cancellationToken);
}
