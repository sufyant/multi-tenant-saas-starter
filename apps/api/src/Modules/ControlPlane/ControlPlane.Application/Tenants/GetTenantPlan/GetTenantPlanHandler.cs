using SharedKernel;

namespace ControlPlane.Application.Tenants.GetTenantPlan;

public static class GetTenantPlanHandler
{
    public static async Task<Result<TenantPlanView>> HandleAsync(GetTenantPlanQuery query, ITenantQueries tenants, TimeProvider time, CancellationToken cancellationToken) =>
        await tenants.FindTenantPlanAsync(time.GetUtcNow(), cancellationToken) is { } plan ? plan : Errors.TenantNotFound;
}
