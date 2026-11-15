namespace Billing.Application.Subscriptions.GetBilling;

public static class GetBillingHandler
{
    public static Task<SubscriptionView?> HandleAsync(GetBillingQuery query, ISubscriptionQueries subscriptions, CancellationToken cancellationToken) =>
        subscriptions.FindSubscriptionAsync(cancellationToken);
}
