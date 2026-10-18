using SharedKernel;

namespace ControlPlane.Application.Tenants.GetTenant;

public static class GetTenantHandler
{
    public static async Task<Result<TenantDetailView>> HandleAsync(
        GetTenantQuery query,
        ITenantQueries tenants,
        TenantOptions settings,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        await tenants.FindTenantDetailAsync(settings.ReopenPeriod, time.GetUtcNow(), cancellationToken) is { } tenant ? tenant : Errors.TenantNotFound;
}
