using Billing.Application.Provider;

namespace Billing.Application.Subscriptions.CancelAtPeriodEnd;

// The handler takes no DbContext, so no transaction is open during the provider's call.
public static class CancelAtPeriodEndHandler
{
    public static Task HandleAsync(CancelAtPeriodEndCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.SetCancelAtPeriodEndAsync(command.SubscriptionId, true, cancellationToken);
}
