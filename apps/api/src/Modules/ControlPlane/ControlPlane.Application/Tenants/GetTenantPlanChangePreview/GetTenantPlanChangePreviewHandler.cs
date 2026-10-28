using ControlPlane.Application.Plans;
using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.Application.Tenants.GetTenantPlanChangePreview;

public static class GetTenantPlanChangePreviewHandler
{
    public static async Task<Result<TenantPlanChangePreviewView>> HandleAsync(
        GetTenantPlanChangePreviewQuery query,
        ITenantQueries tenants,
        IPlanQueries plans,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await plans.FindPlanAsync(query.PlanId, cancellationToken) is not { } plan)
        {
            return Plan.NotFound;
        }

        var values = EntitlementValueView.Read(plan.Entitlements);
        return await tenants.PreviewPlanChangeAsync(values.Value, time.GetUtcNow(), cancellationToken) is { } preview ? preview : Errors.TenantNotFound;
    }
}
