using ControlPlane.Domain.SpendingCaps;

namespace ControlPlane.Application.Usage;

/// <summary>The tenant's spending cap (step 40c). The tenant is the transaction's (R4).</summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface ISpendingCapRepository
{
    /// <summary>The tenant's cap, locked for the rest of the transaction (K6q); none when it has none.</summary>
    internal Task<SpendingCap?> FindForUpdateAsync(CancellationToken cancellationToken);

    internal void Add(SpendingCap cap);

    internal void Remove(SpendingCap cap);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
