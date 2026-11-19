using Billing.Application.Subscriptions;
using Billing.Contracts.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Tenants;

// Reads the payers and the Contract tenants, which belong to no tenant (R6), so it reads across tenants and takes the tenant as an
// argument. Public because Wolverine's generated code builds it on the handler's scope (W1, W9).
public sealed class BillingDirectory(BillingDbContext billing, BillingOptions options) : IBillingDirectory
{
    public bool Enabled => options.Enabled;

    public async Task<IReadOnlyList<Guid>> ListTenantIdsAsync(BillingSituation situation, CancellationToken cancellationToken) =>
        !options.Enabled ? []
        : situation == BillingSituation.Contract ? await billing.ContractTenants.AsNoTracking().Select(listed => listed.TenantId).ToListAsync(cancellationToken)
        : await billing.Payers.AsNoTracking().Where(payer => payer.Overdue).Select(payer => payer.TenantId).ToListAsync(cancellationToken);

    public async Task<BillingSituationCounts?> CountAsync(CancellationToken cancellationToken) =>
        options.Enabled
            ? new BillingSituationCounts(
                await billing.Payers.CountAsync(payer => payer.Overdue, cancellationToken),
                await billing.ContractTenants.CountAsync(cancellationToken))
            : null;

    public async Task<bool> PaysByCardAsync(Guid tenantId, CancellationToken cancellationToken) =>
        options.Enabled
        && await billing.Payers.AnyAsync(payer => payer.TenantId == tenantId, cancellationToken)
        && !await billing.ContractTenants.AnyAsync(listed => listed.TenantId == tenantId, cancellationToken);
}
