using SharedKernel;

namespace Billing.Application.Subscriptions.ApplySubscription;

/// <summary>
/// Takes the provider's state of the tenant's subscription, in the tenant's transaction. An active subscription puts the tenant on the plan
/// it pays for, through ControlPlane (<c>ChangeBilledPlanCommand</c>). The interval and the status travel as their names (N7r).
/// </summary>
/// <param name="TenantId">The tenant the subscription names, whose transaction the message travels in (W3).</param>
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
    bool CancelAtPeriodEnd);
