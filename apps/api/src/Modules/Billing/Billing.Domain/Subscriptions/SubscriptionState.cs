namespace Billing.Domain.Subscriptions;

/// <summary>
/// A subscription as the billing provider holds it at one moment: its customer, the plan it pays for, how often it pays, its state, the end
/// of the period it has paid for, and whether it ends then.
/// </summary>
internal sealed record SubscriptionState(
    string CustomerId,
    string SubscriptionId,
    Guid PlanId,
    BillingInterval Interval,
    SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd);
