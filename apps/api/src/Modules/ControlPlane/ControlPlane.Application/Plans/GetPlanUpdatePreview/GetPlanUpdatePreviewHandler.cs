using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.Application.Plans.GetPlanUpdatePreview;

public static class GetPlanUpdatePreviewHandler
{
    public static async Task<Result<ListPage<TenantExcessView>>> HandleAsync(
        GetPlanUpdatePreviewQuery query,
        IPlanQueries plans,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var values = EntitlementValueView.Read(query.Entitlements);
        if (!values.IsSuccess)
        {
            return values.Error;
        }

        var invalid = values.Value.Select(value => EntitlementValue.For(value.Key, value.Value)).FirstOrDefault(value => !value.IsSuccess);
        if (invalid is not null)
        {
            return invalid.Error;
        }

        return await plans.GetPlanUpdatePreviewAsync(query.PlanId, values.Value, query.List, time.GetUtcNow(), cancellationToken) is { } page
            ? page
            : Plan.NotFound;
    }
}
