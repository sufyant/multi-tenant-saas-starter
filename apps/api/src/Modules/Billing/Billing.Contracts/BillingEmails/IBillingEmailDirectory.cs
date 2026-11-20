namespace Billing.Contracts.BillingEmails;

/// <summary>
/// The tenant's billing email, for the notices that another module raises and Notifications sends to it too (B14; step 40c). It reads in
/// the tenant that the transaction declares, under row level security (R1).
/// </summary>
public interface IBillingEmailDirectory
{
    /// <summary>The tenant's billing email and the member who set it; none without one.</summary>
    Task<BillingEmailRecord?> FindAsync(CancellationToken cancellationToken);
}
