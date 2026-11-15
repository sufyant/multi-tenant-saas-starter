using Billing.Application.Customers;
using Billing.Domain.Customers;
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

    // Two first checkouts at once: the provider's idempotency key gave both the same customer, and the row is the tenant's one. The insert
    // gives way on any of the table's keys, the customer's and the tenant's: an insert that names one key only fails with 23505 on the other
    // when both race (PostgreSQL, "INSERT ... ON CONFLICT", the conflict target). The update that follows waits for the first insert's commit,
    // so each names its member and the last one stays.
    public async Task<string> KeepAsync(string customerId, Guid checkoutBy, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var tenantId = DeclaredTenant;
        await billing.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO billing.customers (customer_id, tenant_id, checkout_by, created_at)
            VALUES ({customerId}, {tenantId}, {checkoutBy}, {at})
            ON CONFLICT DO NOTHING
            """,
            cancellationToken);
        await billing.Database.ExecuteSqlAsync(
            $"UPDATE billing.customers SET checkout_by = {checkoutBy} WHERE tenant_id = {tenantId}", cancellationToken);
        return await billing.Customers.Where(customer => customer.TenantId == tenantId).Select(customer => customer.CustomerId).SingleAsync(cancellationToken);
    }

    // The root of the tenant's billing (B7): locked by its own statement, then read for its change.
    async Task<Customer?> ICustomerStore.FindForUpdateAsync(CancellationToken cancellationToken)
    {
        var tenantId = DeclaredTenant;
        await billing.Database.ExecuteSqlAsync($"SELECT customer_id FROM billing.customers WHERE tenant_id = {tenantId} FOR UPDATE", cancellationToken);
        return await billing.Customers.SingleOrDefaultAsync(customer => customer.TenantId == tenantId, cancellationToken);
    }

    Task ICustomerStore.SaveChangesAsync(CancellationToken cancellationToken) => billing.SaveChangesAsync(cancellationToken);

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
