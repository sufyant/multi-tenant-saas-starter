using Billing.Application.Provider;
using Billing.Application.Subscriptions.SyncSubscription;
using Wolverine;

namespace Billing.Application.PriceChanges.MovePrices;

// The handler takes no DbContext, so no transaction is open during the provider's call. A call that fails is tried again
// (BillingProviderRetries); the move keeps each item's quantity, so a second call changes nothing more.
public static class MovePricesHandler
{
    public static async Task<OutgoingMessages> HandleAsync(MovePricesCommand command, IBillingProvider provider, CancellationToken cancellationToken)
    {
        await provider.MovePricesAsync(command.Order, cancellationToken);
        return [new SyncSubscriptionCommand(command.Order.SubscriptionId)];
    }
}
