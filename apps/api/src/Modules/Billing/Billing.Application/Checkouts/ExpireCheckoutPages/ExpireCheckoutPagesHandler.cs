using Billing.Application.Provider;

namespace Billing.Application.Checkouts.ExpireCheckoutPages;

// The handler takes no DbContext, so no transaction is open during the provider's calls. A call that fails is tried again
// (BillingProviderRetries).
public static class ExpireCheckoutPagesHandler
{
    public static Task HandleAsync(ExpireCheckoutPagesCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.ExpireOpenCheckoutsAsync(command.CustomerId, command.Key, cancellationToken);
}
