using Billing.Application.Provider;

namespace Billing.Application.BillingEmails.KeepCustomerEmail;

// The handler takes no DbContext, so no transaction is open during the provider's call. A call that fails is tried again
// (BillingProviderRetries).
public static class KeepCustomerEmailHandler
{
    public static Task HandleAsync(KeepCustomerEmailCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.SetCustomerEmailAsync(command.CustomerId, command.Email, cancellationToken);
}
