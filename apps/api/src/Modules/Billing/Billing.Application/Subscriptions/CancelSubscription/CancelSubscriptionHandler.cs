using Billing.Application.Provider;

namespace Billing.Application.Subscriptions.CancelSubscription;

// The handler takes no DbContext, so no transaction is open during the provider's call. A call that fails is tried again
// (BillingProviderRetries). A subscription that has ended already changes nothing.
public static class CancelSubscriptionHandler
{
    public static Task HandleAsync(CancelSubscriptionCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.CancelSubscriptionAsync(command.SubscriptionId, cancellationToken);
}
