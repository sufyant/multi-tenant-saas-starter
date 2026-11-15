using Billing.Application.Provider;

namespace Billing.Application.Subscriptions.UndoScheduledChange;

// The handler takes no DbContext, so no transaction is open during the provider's call. A failed call is not retried: the person sees the
// error and asks again.
public static class UndoScheduledChangeHandler
{
    public static Task HandleAsync(UndoScheduledChangeCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.UndoScheduledChangeAsync(command.SubscriptionId, cancellationToken);
}
