using SharedKernel;

namespace Billing.Application.Subscriptions.RouteSubscription;

/// <summary>
/// Finds the tenant of a subscription that the provider holds, by its customer in our own map (B5), and applies the subscription there. A
/// customer that is not in the map is not ours, and nothing follows. The interval and the status travel as their names (N7r). The seats
/// that the fee per person counts are none without a fee per person, and in a message of the version before 40b-backend's seats.
/// </summary>
[MessageName("route-subscription")]
public sealed record RouteSubscriptionCommand(
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
