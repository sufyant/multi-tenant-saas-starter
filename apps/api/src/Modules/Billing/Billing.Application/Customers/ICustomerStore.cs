namespace Billing.Application.Customers;

/// <summary>
/// The map from the provider's customers to their tenants (B5). It belongs to no tenant's row level security, so each method names what it
/// reads: the tenant of the message's transaction (W2) or a customer id from the provider.
/// </summary>
public interface ICustomerStore
{
    /// <summary>The customer of the tenant that the transaction declares; none before its first checkout.</summary>
    Task<string?> FindCustomerIdAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Keeps the customer of the tenant that the transaction declares, when it has none yet, and names the member who opens the checkout.
    /// Answers the tenant's customer, the first one kept when two checkouts raced.
    /// </summary>
    Task<string> KeepAsync(string customerId, Guid checkoutBy, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Deletes the customer of the tenant that the transaction declares, for the tenant's purge; nothing when it has none.</summary>
    Task DeleteAsync(CancellationToken cancellationToken);

    /// <summary>The tenant of the provider's customer and the member who opened its last checkout; none for a customer that is not ours.</summary>
    Task<CustomerTenant?> FindTenantAsync(string customerId, CancellationToken cancellationToken);
}
