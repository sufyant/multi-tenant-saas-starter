namespace Billing.Application.Customers.KeepCustomer;

// A second checkout keeps the first customer, and only names its member: the tenant has one customer.
public static class KeepCustomerHandler
{
    public static Task<string> HandleAsync(KeepCustomerCommand command, ICustomerStore customers, TimeProvider time, CancellationToken cancellationToken) =>
        customers.KeepAsync(command.CustomerId, command.CheckoutBy, time.GetUtcNow(), cancellationToken);
}
