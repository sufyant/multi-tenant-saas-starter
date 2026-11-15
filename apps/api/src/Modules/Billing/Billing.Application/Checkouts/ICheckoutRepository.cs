using Billing.Domain.Checkouts;

namespace Billing.Application.Checkouts;

/// <summary>The tenant's open checkout for the commands, in the tenant that the transaction declares (Q1, R1).</summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface ICheckoutRepository
{
    /// <summary>
    /// The tenant's open checkout, after a lock of the tenant's customer, so two checkouts of one tenant run one after the other, the first
    /// one too (K6a). None when the tenant has none.
    /// </summary>
    internal Task<Checkout?> FindForUpdateAsync(CancellationToken cancellationToken);

    internal void Add(Checkout checkout);

    internal void Remove(Checkout checkout);

    /// <summary>Deletes the tenant's open checkout, for the tenant's purge; nothing when it has none.</summary>
    internal Task DeleteAsync(CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
