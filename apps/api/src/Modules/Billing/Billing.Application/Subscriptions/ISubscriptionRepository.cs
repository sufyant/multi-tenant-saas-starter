using Billing.Domain.Subscriptions;

namespace Billing.Application.Subscriptions;

/// <summary>The subscription aggregate for the commands, in the tenant that the transaction declares (Q1, R1).</summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface ISubscriptionRepository
{
    /// <summary>
    /// The tenant's subscription, locked for the rest of the transaction, so two syncs of one tenant run one after the other (K6a). None
    /// when the tenant has never paid by card.
    /// </summary>
    internal Task<Subscription?> FindForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>Adds the tenant's first subscription; the tenant column takes the declared tenant.</summary>
    internal void Add(Subscription subscription);

    /// <summary>Deletes the tenant's subscription row, for the tenant's purge; nothing when it has none.</summary>
    internal Task DeleteAsync(CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
