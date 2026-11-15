using SharedKernel;

namespace Billing.Application.Customers.KeepCustomer;

/// <summary>
/// Keeps the tenant's customer in our map, when the tenant has none yet, and names the member who opens the checkout, in the tenant of the
/// route (B5). Answers the tenant's customer.
/// </summary>
[MessageName("keep-customer")]
public sealed record KeepCustomerCommand(Guid CheckoutBy, string CustomerId);
