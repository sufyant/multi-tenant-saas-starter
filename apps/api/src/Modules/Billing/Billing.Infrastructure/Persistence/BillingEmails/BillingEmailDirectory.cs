using Billing.Contracts.BillingEmails;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.BillingEmails;

// Row level security keeps the read to the tenant that the transaction declares (R1).
public sealed class BillingEmailDirectory(BillingDbContext billing) : IBillingEmailDirectory
{
    public async Task<BillingEmailRecord?> FindAsync(CancellationToken cancellationToken) =>
        await billing.BillingEmails.AsNoTracking().Select(email => new BillingEmailRecord(email.Address, email.SetBy)).SingleOrDefaultAsync(cancellationToken);
}
