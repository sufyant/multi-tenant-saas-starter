using Billing.Application.Provider;

namespace Billing.Application.Subscriptions.RefundSubscription;

// The handler takes no DbContext, so no transaction is open during the provider's call. A call that fails is tried again
// (BillingProviderRetries), and the refunds' idempotency keys keep a retry from refunding twice.
public static class RefundSubscriptionHandler
{
    public static Task HandleAsync(RefundSubscriptionCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.EndAndRefundAsync(command.SubscriptionId, cancellationToken);
}
