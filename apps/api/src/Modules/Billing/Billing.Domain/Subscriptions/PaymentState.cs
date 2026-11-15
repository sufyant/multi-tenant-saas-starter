namespace Billing.Domain.Subscriptions;

/// <summary>
/// Where a failed payment stands (B11; 10.15 to 10.17): the provider still retries it, its retries are over and billing's grace runs, or the
/// grace ended and the tenant moved to the default plan.
/// </summary>
internal enum PaymentState
{
    Retrying,
    Grace,
    MovedToDefault,
}
