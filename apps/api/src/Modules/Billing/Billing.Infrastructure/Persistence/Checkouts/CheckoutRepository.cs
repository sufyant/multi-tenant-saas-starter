using Billing.Application.Checkouts;
using Billing.Domain.Checkouts;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Checkouts;

/// <summary>The tenant's open checkout for the commands, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class CheckoutRepository(BillingDbContext billing) : ICheckoutRepository
{
    // The tenant's customer row is locked by its own statement, as the first checkout has no row of its own to lock yet; the checkout is
    // then read. Row level security keeps the checkout to the declared tenant, and the customer's read names it (R6).
    async Task<Checkout?> ICheckoutRepository.FindForUpdateAsync(CancellationToken cancellationToken)
    {
        var tenantId = billing.MessageTenantId ?? throw new InvalidOperationException("A tenant's checkout is read in the tenant's transaction.");
        await billing.Database.ExecuteSqlAsync($"SELECT customer_id FROM billing.customers WHERE tenant_id = {tenantId} FOR UPDATE", cancellationToken);
        return await billing.Checkouts.SingleOrDefaultAsync(cancellationToken);
    }

    void ICheckoutRepository.Add(Checkout checkout) => billing.Checkouts.Add(checkout);

    void ICheckoutRepository.Remove(Checkout checkout) => billing.Checkouts.Remove(checkout);

    async Task ICheckoutRepository.DeleteAsync(CancellationToken cancellationToken) =>
        await billing.Checkouts.ExecuteDeleteAsync(cancellationToken);

    Task ICheckoutRepository.SaveChangesAsync(CancellationToken cancellationToken) => billing.SaveChangesAsync(cancellationToken);
}
