namespace Billing.Contracts.Tenants;

/// <summary>A tenant's billing situation that the platform's Organisations list filters by (4.38). The trial joins in step 40d.</summary>
public enum BillingSituation
{
    /// <summary>An open invoice's payment failed (B11): Stripe retries it, the grace runs, or the tenant moved to the default plan.</summary>
    PaymentOverdue,

    /// <summary>The tenant is billed outside the app (B12).</summary>
    Contract,
}
