using Billing.Domain.Payers;
using Billing.Domain.PriceChanges;

namespace Billing.Application.PriceChanges;

/// <summary>
/// The plans' price changes and the tenants' payers (B13). Neither belongs to a tenant's row level security (R6): a method that writes a
/// tenant's payer names the tenant that the message's transaction declares (W2), and the rest name a plan.
/// </summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IPriceChangeStore
{
    /// <summary>Keeps the change once: a second copy of the plan edit's event keeps none, and answers false (I1).</summary>
    internal Task<bool> AddAsync(PriceChange change, CancellationToken cancellationToken);

    internal Task<PriceChange?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The plan's latest change, locked for the rest of the transaction; none before the plan's first.</summary>
    internal Task<PriceChange?> FindLatestForUpdateAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>The plan's latest change, without a lock; none before the plan's first.</summary>
    internal Task<PriceChange?> FindLatestAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>The payers of the plan whose subscription does not end at its period's end.</summary>
    internal Task<IReadOnlyList<Payer>> ListPayersAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>The payer of the tenant that the transaction declares; none while it has no subscription that has not ended.</summary>
    internal Task<Payer?> FindPayerAsync(CancellationToken cancellationToken);

    internal void AddPayer(Payer payer);

    /// <summary>Deletes the payer of the tenant that the transaction declares, for an ended subscription and the tenant's purge.</summary>
    internal Task DeletePayerAsync(CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
