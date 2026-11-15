namespace Billing.Domain.Subscriptions;

/// <summary>How often a subscription pays: every month or every year (step 40).</summary>
internal enum BillingInterval
{
    Monthly,
    Yearly,
}
