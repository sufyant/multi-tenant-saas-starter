using Billing.Application.Provider;

namespace Billing.Application.Subscriptions.SchedulePlanChange;

// The handler takes no DbContext, so no transaction is open during the provider's call. A failed call is not retried: the person sees the
// error and asks again.
public static class SchedulePlanChangeHandler
{
    public static Task HandleAsync(SchedulePlanChangeCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.SchedulePlanChangeAsync(command.Order, cancellationToken);
}
