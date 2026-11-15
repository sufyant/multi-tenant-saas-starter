using SharedKernel;

namespace Billing.Application.Customers.CreateCustomer;

/// <summary>
/// Creates the tenant's customer at the provider, before its first checkout (B5). The provider's idempotency key is the tenant's, so two
/// checkouts at once, or a retry, get the same customer.
/// </summary>
[MessageName("create-customer")]
public sealed record CreateCustomerCommand(Guid TenantId, string? Email);
