using SharedKernel;

namespace Billing.Contracts.Subscriptions;

/// <summary>
/// The tenant's subscription no longer pays for its plan, and the tenant moves to the default plan (B11): the subscription ended at the end of
/// a period that a member cancelled, or the grace of an unpaid one ended (<paramref name="Unpaid"/>). Nothing is deleted (E4). Published
/// through the outbox; ControlPlane moves the tenant in the tenant's transaction, as the tenant's id travels in the envelope (W3). A tenant on
/// the default plan already changes nothing (I1).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="EndedBy">The user id of the member who changed the plan last (B9); the move of an unpaid subscription is nobody's act.</param>
[MessageName("plan-ended")]
public sealed record PlanEndedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid EndedBy, bool Unpaid);
