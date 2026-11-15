using Billing.Application.Customers;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Customers;

/// <summary>
/// The map from the provider's customers to their tenants (B5). It belongs to no tenant's row level security (R6): a read of the tenant of the
/// route names the tenant that the message's transaction declares (W2), and the webhook's read names the customer.
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class CustomerStore(BillingDbContext billing) : ICustomerStore
{
    private Guid DeclaredTenant => billing.MessageTenantId ?? throw new InvalidOperationException("The customer of a tenant is read in the tenant's transaction.");

    public async Task<string?> FindCustomerIdAsync(CancellationToken cancellationToken)
    {
        var tenantId = DeclaredTenant;
        return await billing.Customers.Where(customer => customer.TenantId == tenantId).Select(customer => customer.CustomerId).SingleOrDefaultAsync(cancellationToken);
    }

    // Two first checkouts at once: the provider's idempotency key gave both the same customer, and the tenant's key keeps one row. Each names
    // its member; the last one stays.
    public async Task<string> KeepAsync(string customerId, Guid checkoutBy, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var tenantId = DeclaredTenant;
        await billing.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO billing.customers (customer_id, tenant_id, checkout_by, created_at)
            VALUES ({customerId}, {tenantId}, {checkoutBy}, {at})
            ON CONFLICT (tenant_id) DO UPDATE SET checkout_by = excluded.checkout_by
            """,
            cancellationToken);
        return await billing.Customers.Where(customer => customer.TenantId == tenantId).Select(customer => customer.CustomerId).SingleAsync(cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var tenantId = DeclaredTenant;
        await billing.Customers.Where(customer => customer.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<CustomerTenant?> FindTenantAsync(string customerId, CancellationToken cancellationToken) =>
        await billing.Customers
            .Where(customer => customer.CustomerId == customerId)
            .Select(customer => new CustomerTenant(customer.TenantId, customer.CheckoutBy))
            .SingleOrDefaultAsync(cancellationToken);
}
