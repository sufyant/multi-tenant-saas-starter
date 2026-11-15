using SharedKernel;

namespace Billing.Application.Subscriptions.RouteSubscription;

/// <summary>
/// Finds the tenant of a subscription that the provider holds, by its customer in our own map (B5), and applies the subscription there. A
/// customer that is not in the map is not ours, and nothing follows. The interval and the status travel as their names (N7r).
/// </summary>
[MessageName("route-subscription")]
public sealed record RouteSubscriptionCommand(
    string CustomerId,
    string SubscriptionId,
    Guid PlanId,
    string Interval,
    string Status,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd);
