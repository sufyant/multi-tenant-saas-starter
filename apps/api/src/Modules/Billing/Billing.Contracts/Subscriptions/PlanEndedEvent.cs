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
/// <param name="AmountDue">An unpaid subscription's open invoice without tax, in the currency's minor unit, for the email of the move (13.20);
/// none when the provider holds no failed payment, or in a message of the version before part 10.</param>
/// <param name="Currency">The open invoice's currency, with <paramref name="AmountDue"/>.</param>
/// <param name="BillingEmail">The tenant's billing email, which billing's emails go to besides its members (B14); none without one.</param>
/// <param name="BillingEmailSetBy">The user id of the member who set the billing email, whose language and time zone its email takes.</param>
[MessageName("plan-ended")]
public sealed record PlanEndedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid EndedBy,
    bool Unpaid,
    long? AmountDue = null,
    string? Currency = null,
    string? BillingEmail = null,
    Guid? BillingEmailSetBy = null);
