using Billing.Application.Provider;

namespace Billing.Application.Subscriptions.GetPlanChangePreview;

// The handler takes no DbContext, so no transaction is open during the provider's call. A failed call is not retried: the person sees the
// error and asks again.
public static class GetPlanChangePreviewHandler
{
    public static Task<PlanChangePreview> HandleAsync(GetPlanChangePreviewQuery query, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.PreviewPlanChangeAsync(query.Order, query.ProratedAt, cancellationToken);
}
