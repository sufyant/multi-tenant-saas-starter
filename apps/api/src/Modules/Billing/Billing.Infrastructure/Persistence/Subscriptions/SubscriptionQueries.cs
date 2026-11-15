using Billing.Application.Subscriptions.GetBilling;
using Billing.Application.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Subscriptions;

/// <summary>The tenant's subscription for the queries, read without tracking in the declared tenant (Q2, R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class SubscriptionQueries(BillingDbContext billing) : ISubscriptionQueries
{
    // The payment's state is the aggregate's own reading (B11), so the row is read whole and mapped here.
    public async Task<SubscriptionView?> FindSubscriptionAsync(CancellationToken cancellationToken) =>
        await billing.Subscriptions.AsNoTracking().SingleOrDefaultAsync(cancellationToken) is { } s
            ? new SubscriptionView(
                s.CustomerId, s.SubscriptionId, s.PlanId, s.Interval.ToString(), s.Status.ToString(), s.CurrentPeriodEnd, s.CancelAtPeriodEnd,
                s.ScheduledPlanId, s.ScheduledInterval?.ToString(),
                s is { PaymentState: { } state, FailedPayment: { } failed }
                    ? new PaymentView(
                        state.ToString(), failed.FailedAt, failed.Attempts, failed.NextAttemptAt, s.GraceEndsAt, s.MovedToDefaultAt, failed.AmountDue,
                        failed.Currency, failed.CardBrand, failed.CardLast4)
                    : null)
            : null;
}
