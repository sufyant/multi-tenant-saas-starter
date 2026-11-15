using Billing.Application.Subscriptions.GetBilling;

namespace Billing.Application.Subscriptions;

/// <summary>The tenant's subscription for the queries, read without tracking in the tenant of the message (Q2, R1).</summary>
public interface ISubscriptionQueries
{
    /// <summary>The subscription of the tenant that the transaction declares; none when it has never paid by card.</summary>
    Task<SubscriptionView?> FindSubscriptionAsync(CancellationToken cancellationToken);
}
