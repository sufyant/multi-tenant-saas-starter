namespace Billing.Application.Provider;

/// <summary>
/// The extra of one meter in one month of UTC, which the subscription's next invoice charges (E11): <paramref name="Extra"/> units over the
/// quota, which cost <paramref name="Amount"/> minor units of <paramref name="Currency"/> at the price the tenant saw. The tenant, the meter
/// and the month name the charge, so one month's extra of a meter is charged once.
/// </summary>
public sealed record ExtraCharge(
    string CustomerId,
    string SubscriptionId,
    Guid TenantId,
    string MeterKey,
    DateOnly Period,
    long Extra,
    long Amount,
    string Currency);
