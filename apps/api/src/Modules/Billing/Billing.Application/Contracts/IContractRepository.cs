using Billing.Domain.Contracts;

namespace Billing.Application.Contracts;

/// <summary>The tenant's Contract mark (B12), in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IContractRepository
{
    /// <summary>
    /// The tenant's mark, locked for the rest of the transaction; none for a tenant that pays by card. A caller that changes billing locks
    /// the tenant's customer first, when it has one (B7).
    /// </summary>
    internal Task<Contract?> FindForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>Whether the tenant is Contract, without a lock, for a read or a check before any change.</summary>
    internal Task<bool> IsContractAsync(CancellationToken cancellationToken);

    internal void Add(Contract contract);

    internal void Remove(Contract contract);

    /// <summary>Deletes the tenant's mark, for the tenant's purge; nothing when it has none.</summary>
    internal Task DeleteAsync(CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
