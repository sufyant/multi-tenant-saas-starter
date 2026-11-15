using Billing.Application.Provider;

namespace Billing.Application.Checkouts.ExpireOtherCheckouts;

// The handler takes no DbContext, so no transaction is open during the provider's calls. A call that fails is tried again
// (BillingProviderRetries).
public static class ExpireOtherCheckoutsHandler
{
    public static Task HandleAsync(ExpireOtherCheckoutsCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.ExpireOpenCheckoutsAsync(command.CustomerId, command.KeepKey, cancellationToken);
}
