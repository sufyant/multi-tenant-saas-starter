using SharedKernel;

namespace Billing.Contracts.Subscriptions;

/// <summary>
/// A member scheduled a move of the tenant's subscription to <paramref name="NewPlanId"/> at <paramref name="AppliesAt"/>, the end of the paid period (B9). The same plan twice is a move from yearly to monthly. Published through the outbox once the provider's state shows it, with the names of that moment, which the Activity log
/// keeps (K3, 12.12); the tenant's id travels in the envelope too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ChangedBy">The user id of the member who changed it.</param>
/// <param name="PlanId">The plan the subscription pays for.</param>
[MessageName("plan-change-scheduled")]
public sealed record PlanChangeScheduledEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid ChangedBy,
    Guid PlanId,
    string PlanName,
    Guid NewPlanId,
    string NewPlanName,
    DateTimeOffset AppliesAt);
