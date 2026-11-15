namespace Billing.Application.Subscriptions.GetBilling;

/// <summary>
/// A tenant's subscription as billing last read it from the provider, with the move a member scheduled at the period's end (B9) and a failed
/// payment's dunning (B11). The interval and the status cross to the API as their names (N7r).
/// </summary>
public sealed record SubscriptionView(
    string CustomerId,
    string SubscriptionId,
    Guid PlanId,
    string Interval,
    string Status,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    Guid? ScheduledPlanId,
    string? ScheduledInterval,
    PaymentView? Payment = null);
