using Billing.Application.Checkouts;
using Billing.Domain.Checkouts;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Checkouts;

/// <summary>The tenant's open checkout for the commands, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class CheckoutRepository(BillingDbContext billing) : ICheckoutRepository
{
    // The caller locked the tenant's customer, the root of its billing, first (B7); the checkout is then locked and read. Row level security
    // keeps it to the declared tenant.
    async Task<Checkout?> ICheckoutRepository.FindForUpdateAsync(CancellationToken cancellationToken)
    {
        await billing.Database.ExecuteSqlAsync($"SELECT tenant_id FROM billing.checkouts FOR UPDATE", cancellationToken);
        return await billing.Checkouts.SingleOrDefaultAsync(cancellationToken);
    }

    void ICheckoutRepository.Add(Checkout checkout) => billing.Checkouts.Add(checkout);

    void ICheckoutRepository.Remove(Checkout checkout) => billing.Checkouts.Remove(checkout);

    async Task ICheckoutRepository.DeleteAsync(CancellationToken cancellationToken) =>
        await billing.Checkouts.ExecuteDeleteAsync(cancellationToken);

    Task ICheckoutRepository.SaveChangesAsync(CancellationToken cancellationToken) => billing.SaveChangesAsync(cancellationToken);
}
