using Billing.Application.Contracts;
using Billing.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Contracts;

/// <summary>The tenant's Contract mark, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class ContractRepository(BillingDbContext billing) : IContractRepository
{
    async Task<Contract?> IContractRepository.FindForUpdateAsync(CancellationToken cancellationToken)
    {
        await billing.Database.ExecuteSqlAsync($"SELECT tenant_id FROM billing.contracts FOR UPDATE", cancellationToken);
        return await billing.Contracts.SingleOrDefaultAsync(cancellationToken);
    }

    Task<bool> IContractRepository.IsContractAsync(CancellationToken cancellationToken) => billing.Contracts.AnyAsync(cancellationToken);

    void IContractRepository.Add(Contract contract) => billing.Contracts.Add(contract);

    void IContractRepository.Remove(Contract contract) => billing.Contracts.Remove(contract);

    async Task IContractRepository.DeleteAsync(CancellationToken cancellationToken) => await billing.Contracts.ExecuteDeleteAsync(cancellationToken);

    Task IContractRepository.SaveChangesAsync(CancellationToken cancellationToken) => billing.SaveChangesAsync(cancellationToken);
}
