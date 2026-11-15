using SharedKernel;

namespace Billing.Application.Customers.GetCustomer;

/// <summary>The customer at the provider of the tenant of the route; none before its first checkout (B5).</summary>
[MessageName("get-customer")]
public sealed record GetCustomerQuery;
