namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// The tenant's subscription as billing last read it from the provider: the plan it pays for, how often, its state, the end of the period it
/// has paid for, and whether it ends then: a member's cancellation, which moves the tenant to the default plan then (B9), or a closure's
/// (B8). <c>scheduledChange</c> is the move to another plan or interval that a member scheduled at <c>currentPeriodEnd</c> (10.07), or
/// null. <c>payment</c> is a failed payment's dunning (B11), or null when nothing is due.
/// </summary>
public sealed record SubscriptionResponse(
    Guid PlanId,
    SubscriptionResponseInterval Interval,
    SubscriptionResponseStatus Status,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    ScheduledChangeResponse? ScheduledChange,
    PaymentResponse? Payment);
