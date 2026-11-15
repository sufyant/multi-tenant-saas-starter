namespace Billing.Domain.Customers;

/// <summary>
/// The map from a customer at the billing provider to its tenant, in our own data: a webhook names a customer, and only this row says which
/// tenant it is (B5). The provider's metadata names the tenant too, for support, and is never read for it (the coordinator's choice of
/// 2026-11-15: anyone with access to the provider's dashboard can edit metadata, so tenant isolation stays in our database). One customer for
/// each tenant. It belongs to no tenant's row level security, because the webhook reads it before any tenant is known (R6).
/// </summary>
internal sealed class Customer(string customerId, Guid tenantId, Guid checkoutBy, DateTimeOffset createdAt)
{
    public string CustomerId { get; private init; } = customerId;

    public Guid TenantId { get; private init; } = tenantId;

    /// <summary>The member who opened the tenant's last checkout, whom a subscription's change names as the one who paid.</summary>
    public Guid CheckoutBy { get; private set; } = checkoutBy;

    public DateTimeOffset CreatedAt { get; private init; } = createdAt;
}
