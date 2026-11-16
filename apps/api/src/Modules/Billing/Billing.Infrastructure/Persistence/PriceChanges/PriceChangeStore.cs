using Billing.Application.PriceChanges;
using Billing.Domain.Payers;
using Billing.Domain.PriceChanges;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.PriceChanges;

/// <summary>The plans' price changes and the tenants' payers, which belong to no tenant's row level security (R6).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class PriceChangeStore(BillingDbContext billing) : IPriceChangeStore
{
    private Guid DeclaredTenant => billing.MessageTenantId ?? throw new InvalidOperationException("A tenant's payer is written in the tenant's transaction.");

    async Task<bool> IPriceChangeStore.AddAsync(PriceChange change, CancellationToken cancellationToken)
    {
        if (await billing.PriceChanges.AnyAsync(row => row.Id == change.Id, cancellationToken))
        {
            return false;
        }

        billing.PriceChanges.Add(change);
        return true;
    }

    async Task<PriceChange?> IPriceChangeStore.FindAsync(Guid id, CancellationToken cancellationToken) =>
        await billing.PriceChanges.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    async Task<PriceChange?> IPriceChangeStore.FindLatestForUpdateAsync(Guid planId, CancellationToken cancellationToken)
    {
        await billing.Database.ExecuteSqlAsync(
            $"SELECT id FROM billing.price_changes WHERE plan_id = {planId} ORDER BY changed_at DESC, id DESC LIMIT 1 FOR UPDATE", cancellationToken);
        return await Latest(planId).FirstOrDefaultAsync(cancellationToken);
    }

    async Task<PriceChange?> IPriceChangeStore.FindLatestAsync(Guid planId, CancellationToken cancellationToken) =>
        await Latest(planId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

    async Task<IReadOnlyList<Payer>> IPriceChangeStore.ListPayersAsync(Guid planId, CancellationToken cancellationToken) =>
        await billing.Payers.AsNoTracking().Where(payer => payer.PlanId == planId && !payer.Ends).OrderBy(payer => payer.TenantId).ToListAsync(cancellationToken);

    async Task<Payer?> IPriceChangeStore.FindPayerAsync(CancellationToken cancellationToken)
    {
        var tenantId = DeclaredTenant;
        return await billing.Payers.SingleOrDefaultAsync(payer => payer.TenantId == tenantId, cancellationToken);
    }

    void IPriceChangeStore.AddPayer(Payer payer) => billing.Payers.Add(payer);

    async Task IPriceChangeStore.DeletePayerAsync(CancellationToken cancellationToken)
    {
        var tenantId = DeclaredTenant;
        await billing.Payers.Where(payer => payer.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
    }

    Task IPriceChangeStore.SaveChangesAsync(CancellationToken cancellationToken) => billing.SaveChangesAsync(cancellationToken);

    private IQueryable<PriceChange> Latest(Guid planId) =>
        billing.PriceChanges.Where(row => row.PlanId == planId).OrderByDescending(row => row.ChangedAt).ThenByDescending(row => row.Id);
}
