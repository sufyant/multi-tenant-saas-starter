using Billing.Application.Provider;

namespace Billing.Application.Subscriptions.SetSeats;

// The handler takes no DbContext, so no transaction is open during the provider's call. A call that fails is tried again
// (BillingProviderRetries).
public static class SetSeatsHandler
{
    public static Task HandleAsync(SetSeatsCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.SetSeatsAsync(command.SubscriptionId, command.People, cancellationToken);
}
