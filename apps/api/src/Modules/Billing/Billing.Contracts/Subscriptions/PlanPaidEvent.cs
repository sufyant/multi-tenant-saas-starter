using SharedKernel;

namespace Billing.Contracts.Subscriptions;

/// <summary>
/// A tenant's subscription is active and pays for this plan (step 40). Published through the outbox; ControlPlane puts the tenant on the plan
/// in the tenant's transaction, as the tenant's id travels in the message's envelope ("Billing changes a tenant's plan through
/// ControlPlane", <c>docs/roadmap.md</c>, section 8). It comes again at each change of the subscription while it is active, so a subscriber
/// changes nothing for the plan the tenant is on (I1).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="PaidBy">The user id of the member who paid for the plan.</param>
[MessageName("plan-paid")]
public sealed record PlanPaidEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid PlanId, Guid PaidBy);
