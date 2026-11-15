using Billing.Application.Provider;

namespace Billing.Application.Customers.CreateCustomer;

// The handler takes no DbContext, so no transaction is open during the provider's call (section 3, "Handlers and pipeline").
public static class CreateCustomerHandler
{
    public static Task<string> HandleAsync(CreateCustomerCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.CreateCustomerAsync(command.TenantId, command.Email, cancellationToken);
}
