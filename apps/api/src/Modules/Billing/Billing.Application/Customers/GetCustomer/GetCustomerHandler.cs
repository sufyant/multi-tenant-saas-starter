namespace Billing.Application.Customers.GetCustomer;

public static class GetCustomerHandler
{
    public static Task<string?> HandleAsync(GetCustomerQuery query, ICustomerStore customers, CancellationToken cancellationToken) =>
        customers.FindCustomerIdAsync(cancellationToken);
}
