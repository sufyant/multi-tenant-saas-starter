namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// The tenant's subscription as billing last read it from the provider: the plan it pays for, how often, its state, the end of the period it
/// has paid for, and whether it ends then.
/// </summary>
public sealed record SubscriptionResponse(
    Guid PlanId,
    SubscriptionResponseInterval Interval,
    SubscriptionResponseStatus Status,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd);
