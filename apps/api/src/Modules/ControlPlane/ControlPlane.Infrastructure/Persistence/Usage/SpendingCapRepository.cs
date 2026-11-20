using ControlPlane.Application.Usage;
using ControlPlane.Domain.SpendingCaps;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Usage;

/// <summary>The tenant's spending cap, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class SpendingCapRepository(ControlPlaneDbContext controlPlane) : ISpendingCapRepository
{
    // Row level security keeps the read to the tenant's one row. A missing row locks nothing: a cap's first set holds the tenant row
    // FOR UPDATE, which every use holds FOR SHARE (K6q).
    async Task<SpendingCap?> ISpendingCapRepository.FindForUpdateAsync(CancellationToken cancellationToken) =>
        await controlPlane.SpendingCaps.FromSql($"SELECT * FROM control_plane.spending_caps FOR UPDATE").SingleOrDefaultAsync(cancellationToken);

    void ISpendingCapRepository.Add(SpendingCap cap) => controlPlane.SpendingCaps.Add(cap);

    void ISpendingCapRepository.Remove(SpendingCap cap) => controlPlane.SpendingCaps.Remove(cap);

    Task ISpendingCapRepository.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
