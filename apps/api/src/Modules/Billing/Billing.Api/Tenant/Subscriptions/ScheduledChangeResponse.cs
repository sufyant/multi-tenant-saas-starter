namespace Billing.Api.Tenant.Subscriptions;

/// <summary>The plan and the interval that the subscription moves to at the end of its paid period, by a member's wish (B9).</summary>
public sealed record ScheduledChangeResponse(Guid PlanId, SubscriptionResponseInterval Interval);
