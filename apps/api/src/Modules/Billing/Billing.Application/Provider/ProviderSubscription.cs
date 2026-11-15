namespace Billing.Application.Provider;

/// <summary>
/// A subscription at the provider: its customer, the plan it pays for, which the provider names by our plan's Product, and its state. The
/// status is the name of a <c>SubscriptionStatus</c> (N7r), which the adapter maps from the provider's own word. It names no tenant: our own
/// map of the customers does (B5). A move that a member scheduled at the period's end names its plan and interval; none when nothing is
/// scheduled (B9). <paramref name="People"/> is how many seated members the fee per person counts; none when the subscription has no fee per
/// person (step 40, billing per person).
/// </summary>
public sealed record ProviderSubscription(
    string CustomerId,
    string SubscriptionId,
    Guid PlanId,
    bool Yearly,
    string Status,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    Guid? ScheduledPlanId = null,
    bool? ScheduledYearly = null,
    int? People = null);
