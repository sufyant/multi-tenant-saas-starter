using Billing.Application.BillingEmails;
using Billing.Application.BillingEmails.GetBillingEmail;
using Billing.Domain.BillingEmails;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.BillingEmails;

/// <summary>The tenant's billing email, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class BillingEmailRepository(BillingDbContext billing) : IBillingEmailRepository
{
    // A missing row locks nothing, and a tenant with no customer has nothing else to lock, so the lock is a transaction-level advisory lock
    // of the tenant's billing email, as the Contract mark's (B12): two first addresses at once run one after the other.
    async Task<BillingEmail?> IBillingEmailRepository.FindForUpdateAsync(CancellationToken cancellationToken)
    {
        await billing.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended('billing.billing_emails:' || current_setting('app.tenant_id'), 0))", cancellationToken);
        return await billing.BillingEmails.SingleOrDefaultAsync(cancellationToken);
    }

    async Task<BillingEmailView?> IBillingEmailRepository.ReadAsync(CancellationToken cancellationToken) =>
        await billing.BillingEmails.AsNoTracking().Select(email => new BillingEmailView(email.Address, email.SetBy)).SingleOrDefaultAsync(cancellationToken);

    void IBillingEmailRepository.Add(BillingEmail email) => billing.BillingEmails.Add(email);

    void IBillingEmailRepository.Remove(BillingEmail email) => billing.BillingEmails.Remove(email);

    async Task IBillingEmailRepository.DeleteAsync(CancellationToken cancellationToken) => await billing.BillingEmails.ExecuteDeleteAsync(cancellationToken);

    Task IBillingEmailRepository.SaveChangesAsync(CancellationToken cancellationToken) => billing.SaveChangesAsync(cancellationToken);
}
