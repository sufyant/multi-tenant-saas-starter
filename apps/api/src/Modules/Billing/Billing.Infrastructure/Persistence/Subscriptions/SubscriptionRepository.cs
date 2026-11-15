using Billing.Application.Subscriptions;
using Billing.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Subscriptions;

/// <summary>The tenant's subscription for the commands, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class SubscriptionRepository(BillingDbContext billing) : ISubscriptionRepository
{
    // The row is locked by its own statement, then read, so the lock never sits in a subquery that EF Core composes. Row level security
    // keeps both to the declared tenant.
    async Task<Subscription?> ISubscriptionRepository.FindForUpdateAsync(CancellationToken cancellationToken)
    {
        await billing.Database.ExecuteSqlAsync($"SELECT tenant_id FROM billing.subscriptions FOR UPDATE", cancellationToken);
        return await billing.Subscriptions.SingleOrDefaultAsync(cancellationToken);
    }

    void ISubscriptionRepository.Add(Subscription subscription) => billing.Subscriptions.Add(subscription);

    // Row level security keeps the statement to the declared tenant.
    async Task ISubscriptionRepository.DeleteAsync(CancellationToken cancellationToken) =>
        await billing.Subscriptions.ExecuteDeleteAsync(cancellationToken);

    Task ISubscriptionRepository.SaveChangesAsync(CancellationToken cancellationToken) => billing.SaveChangesAsync(cancellationToken);
}
