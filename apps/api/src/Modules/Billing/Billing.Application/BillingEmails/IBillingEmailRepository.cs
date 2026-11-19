using Billing.Application.BillingEmails.GetBillingEmail;
using Billing.Domain.BillingEmails;

namespace Billing.Application.BillingEmails;

/// <summary>The tenant's billing email (B14), in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IBillingEmailRepository
{
    /// <summary>The tenant's billing email and who set it, without a lock, for a read or for the recipients of an event.</summary>
    Task<BillingEmailView?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The tenant's billing email, with its row locked for the rest of the transaction; none when it has none. A caller locks the tenant's
    /// customer first, when it has one (B7).
    /// </summary>
    internal Task<BillingEmail?> FindForUpdateAsync(CancellationToken cancellationToken);


    internal void Add(BillingEmail email);

    internal void Remove(BillingEmail email);

    /// <summary>Deletes the tenant's billing email, for the tenant's purge; nothing when it has none.</summary>
    internal Task DeleteAsync(CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
