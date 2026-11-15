namespace Billing.Domain.Subscriptions;

/// <summary>
/// A subscription as the billing provider holds it at one moment: its customer, the plan it pays for, how often it pays, its state, the end
/// of the period it has paid for, and whether it ends then. A move to another plan or interval that a member asked for at the period's end
/// (B9) names the plan and the interval it moves to; none when nothing is scheduled.
/// </summary>
internal sealed record SubscriptionState(
    string CustomerId,
    string SubscriptionId,
    Guid PlanId,
    BillingInterval Interval,
    SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    Guid? ScheduledPlanId = null,
    BillingInterval? ScheduledInterval = null);
