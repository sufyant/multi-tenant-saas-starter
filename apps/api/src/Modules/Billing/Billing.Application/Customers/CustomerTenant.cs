namespace Billing.Application.Customers;

/// <summary>The tenant of a customer, and the member who opened its last checkout.</summary>
public sealed record CustomerTenant(Guid TenantId, Guid CheckoutBy);
