using Billing.Application.Provider;

namespace Billing.Application.Subscriptions.SetCancelAtPeriodEnd;

// The handler takes no DbContext, so no transaction is open during the provider's call. A call that fails is tried again
// (BillingProviderRetries).
public static class SetCancelAtPeriodEndHandler
{
    public static Task HandleAsync(SetCancelAtPeriodEndCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.SetCancelAtPeriodEndAsync(command.SubscriptionId, command.CancelAtPeriodEnd, cancellationToken);
}
