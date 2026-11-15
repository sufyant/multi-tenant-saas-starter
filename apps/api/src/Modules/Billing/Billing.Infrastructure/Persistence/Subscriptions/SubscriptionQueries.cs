using Billing.Application.Subscriptions.GetBilling;
using Billing.Application.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Subscriptions;

/// <summary>The tenant's subscription for the queries, read without tracking in the declared tenant (Q2, R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class SubscriptionQueries(BillingDbContext billing) : ISubscriptionQueries
{
    public async Task<SubscriptionView?> FindSubscriptionAsync(CancellationToken cancellationToken) =>
        await billing.Subscriptions
            .AsNoTracking()
            .Select(s => new SubscriptionView(s.CustomerId, s.SubscriptionId, s.PlanId, s.Interval.ToString(), s.Status.ToString(), s.CurrentPeriodEnd, s.CancelAtPeriodEnd))
            .SingleOrDefaultAsync(cancellationToken);
}
