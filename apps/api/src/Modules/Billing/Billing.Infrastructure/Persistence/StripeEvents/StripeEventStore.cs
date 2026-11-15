using Billing.Application.StripeEvents;
using Billing.Domain.StripeEvents;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.StripeEvents;

/// <summary>The provider's events that billing received. They belong to no tenant, so no row level security applies (R6).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class StripeEventStore(BillingDbContext billing) : IStripeEventStore
{
    // Two deliveries of one event may race; the key keeps one row, and only the first insert counts.
    async Task<bool> IStripeEventStore.AddAsync(StripeEvent stripeEvent, CancellationToken cancellationToken) =>
        await billing.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO billing.stripe_events (id, type, received_at)
            VALUES ({stripeEvent.Id}, {stripeEvent.Type}, {stripeEvent.ReceivedAt})
            ON CONFLICT (id) DO NOTHING
            """,
            cancellationToken) == 1;
}
