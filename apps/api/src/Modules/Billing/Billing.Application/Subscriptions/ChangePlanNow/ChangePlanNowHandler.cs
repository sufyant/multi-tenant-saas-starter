using Billing.Application.Provider;

namespace Billing.Application.Subscriptions.ChangePlanNow;

// The handler takes no DbContext, so no transaction is open during the provider's call. A failed call is not retried: the person sees the
// error and asks again, and the provider charged nothing for a change it did not make.
public static class ChangePlanNowHandler
{
    public static Task<bool> HandleAsync(ChangePlanNowCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.ChangePlanNowAsync(command.Order, command.ProratedAt, cancellationToken);
}
