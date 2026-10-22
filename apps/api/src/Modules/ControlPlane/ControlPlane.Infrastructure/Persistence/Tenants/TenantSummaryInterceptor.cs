using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

// K6k: a transaction that saved a change to a membership or an invitation commits only after the tenant's summary followed it, so the
// summary always says what the rows say (R12). ITenantRepository.SaveAsync writes it; a command that forgets it fails here, and its
// transaction rolls back.
internal sealed class TenantSummaryInterceptor : DbTransactionInterceptor
{
    public static TenantSummaryInterceptor Instance { get; } = new();

    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        Check(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        Check(eventData);
        return ValueTask.FromResult(result);
    }

    private static void Check(TransactionEventData eventData)
    {
        if (eventData.Context is ControlPlaneDbContext { SummaryOwed: true })
        {
            throw new InvalidOperationException(
                "A change to a tenant's memberships or invitations is saved with ITenantRepository.SaveAsync, which writes the tenant's summary (K6k).");
        }
    }
}
