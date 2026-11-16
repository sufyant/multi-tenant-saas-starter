using SharedKernel;

namespace Billing.Contracts.PriceChanges;

/// <summary>
/// A system admin moved the payers of a plan to its new prices from a day (B13, 7.11): every payer is told 30 days before it. Published
/// without a tenant, as a plan belongs to none, with the plan's name of that moment, which the platform's log keeps (K3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ScheduledBy">The user id of the system admin.</param>
[MessageName("price-change-scheduled")]
public sealed record PriceChangeScheduledEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PlanId,
    string PlanName,
    Guid ScheduledBy,
    string Currency,
    PriceChangePricesRecord OldPrices,
    PriceChangePricesRecord NewPrices,
    DateTimeOffset MovesAt);
