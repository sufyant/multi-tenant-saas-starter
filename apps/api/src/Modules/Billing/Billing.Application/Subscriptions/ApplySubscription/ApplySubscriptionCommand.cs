using SharedKernel;

namespace Billing.Application.Subscriptions.ApplySubscription;

/// <summary>
/// Takes the provider's state of the tenant's subscription, in the tenant's transaction. An active subscription puts the tenant on the plan
/// it pays for, through ControlPlane (<c>ChangeBilledPlanCommand</c>). The interval and the status travel as their names (N7r).
/// </summary>
/// <param name="TenantId">The tenant the subscription names, whose transaction the message travels in (W3).</param>
/// <param name="ScheduledPlanId">The plan of a move that a member scheduled at the period's end, with its interval (B9); none in a message of
/// the version before 40b-backend's plan changes.</param>
/// <param name="People">How many seated members the fee per person counts at the provider; none without a fee per person, and in a
/// message of the version before 40b-backend's seats.</param>
/// <param name="PaidBy">The user id of the member who paid for the subscription.</param>
[MessageName("apply-subscription")]
public sealed record ApplySubscriptionCommand(
    Guid TenantId,
    Guid PaidBy,
    string CustomerId,
    string SubscriptionId,
    Guid PlanId,
    string Interval,
    string Status,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    Guid? ScheduledPlanId = null,
    string? ScheduledInterval = null,
    int? People = null);
