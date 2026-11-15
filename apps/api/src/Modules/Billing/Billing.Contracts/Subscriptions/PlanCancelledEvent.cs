using SharedKernel;

namespace Billing.Contracts.Subscriptions;

/// <summary>
/// A member cancelled the tenant's subscription (B9): the tenant moves to the default plan, <paramref name="NewPlanId"/>, at <paramref name="MovesAt"/>, the end of the paid period. Published through the outbox once the provider's state shows it, with the names of that moment, which the Activity log
/// keeps (K3, 12.12); the tenant's id travels in the envelope too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ChangedBy">The user id of the member who changed it.</param>
/// <param name="PlanId">The plan the subscription pays for.</param>
[MessageName("plan-cancelled")]
public sealed record PlanCancelledEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid ChangedBy,
    Guid PlanId,
    string PlanName,
    Guid NewPlanId,
    string NewPlanName,
    DateTimeOffset MovesAt);
