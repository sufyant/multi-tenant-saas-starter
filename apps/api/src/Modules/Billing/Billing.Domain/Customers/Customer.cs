namespace Billing.Domain.Customers;

/// <summary>
/// The map from a customer at the billing provider to its tenant, in our own data: a webhook names a customer, and only this row says which
/// tenant it is (B5). The provider's metadata names the tenant too, for support, and is never read for it (the coordinator's choice of
/// 2026-11-15: anyone with access to the provider's dashboard can edit metadata, so tenant isolation stays in our database). One customer for
/// each tenant. It is the root of the tenant's billing: a change of the tenant's subscription or checkout locks it first (K6a, B7). It
/// belongs to no tenant's row level security, because the webhook reads it before any tenant is known (R6).
/// </summary>
internal sealed class Customer(string customerId, Guid tenantId, Guid checkoutBy, DateTimeOffset createdAt)
{
    public string CustomerId { get; private init; } = customerId;

    public Guid TenantId { get; private init; } = tenantId;

    /// <summary>
    /// The member who opened the tenant's last checkout or changed its plan last (B9), whom a subscription's change names as the one who
    /// paid.
    /// </summary>
    public Guid CheckoutBy { get; private set; } = checkoutBy;

    public DateTimeOffset CreatedAt { get; private init; } = createdAt;

    /// <summary>When the tenant was closed; none while it is open. A closed tenant opens no checkout and pays for nothing more (B8).</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>The moment of the last closure or reopen that billing took, so an older one that arrives later changes nothing.</summary>
    public DateTimeOffset? LifecycleAt { get; private set; }

    /// <summary>Names the member who changes the tenant's plan, so the change that the provider's state then brings names them (B6, B9).</summary>
    public void NameMember(Guid memberId) => CheckoutBy = memberId;

    /// <summary>Takes the tenant's closure of this moment; false for one older than the last closure or reopen billing took (one of the same moment is taken: the clock cannot order them).</summary>
    public bool Close(DateTimeOffset at)
    {
        if (LifecycleAt > at)
        {
            return false;
        }

        (ClosedAt, LifecycleAt) = (at, at);
        return true;
    }

    /// <summary>Takes the tenant's reopen of this moment; false for one older than the last closure or reopen billing took (one of the same moment is taken: the clock cannot order them).</summary>
    public bool Reopen(DateTimeOffset at)
    {
        if (LifecycleAt > at)
        {
            return false;
        }

        (ClosedAt, LifecycleAt) = (null, at);
        return true;
    }
}
