using Billing.Application.Provider;

namespace Billing.Application.Checkouts.ExpireCheckoutSession;

// The handler takes no DbContext, so no transaction is open during the provider's call. A call that fails is tried again
// (BillingProviderRetries).
public static class ExpireCheckoutSessionHandler
{
    public static Task HandleAsync(ExpireCheckoutSessionCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.ExpireCheckoutAsync(command.SessionId, cancellationToken);
}
