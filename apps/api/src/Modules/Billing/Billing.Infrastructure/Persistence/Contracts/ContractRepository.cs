using Billing.Application.Contracts;
using Billing.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Contracts;

/// <summary>The tenant's Contract mark, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class ContractRepository(BillingDbContext billing) : IContractRepository
{
    private Guid DeclaredTenant => billing.MessageTenantId ?? throw new InvalidOperationException("A tenant's mark is written in the tenant's transaction.");

    // A missing row locks nothing, and a tenant that never paid by card has no customer to lock, so the lock is a transaction-level
    // advisory lock of the tenant's mark (B12): two switches, or a switch and a first checkout, run one after the other.
    async Task<Contract?> IContractRepository.FindForUpdateAsync(CancellationToken cancellationToken)
    {
        await billing.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended('billing.contracts:' || current_setting('app.tenant_id'), 0))", cancellationToken);
        return await billing.Contracts.SingleOrDefaultAsync(cancellationToken);
    }

    Task<bool> IContractRepository.IsContractAsync(CancellationToken cancellationToken) => billing.Contracts.AnyAsync(cancellationToken);

    // The tenant's listing across tenants (4.38) follows the mark, in the same transaction.
    void IContractRepository.Add(Contract contract)
    {
        billing.Contracts.Add(contract);
        billing.ContractTenants.Add(new ContractTenant(DeclaredTenant));
    }

    void IContractRepository.Remove(Contract contract)
    {
        billing.Contracts.Remove(contract);
        billing.ContractTenants.Remove(new ContractTenant(DeclaredTenant));
    }

    async Task IContractRepository.DeleteAsync(CancellationToken cancellationToken)
    {
        var tenantId = DeclaredTenant;
        await billing.Contracts.ExecuteDeleteAsync(cancellationToken);
        await billing.ContractTenants.Where(listed => listed.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
    }

    Task IContractRepository.SaveChangesAsync(CancellationToken cancellationToken) => billing.SaveChangesAsync(cancellationToken);
}
