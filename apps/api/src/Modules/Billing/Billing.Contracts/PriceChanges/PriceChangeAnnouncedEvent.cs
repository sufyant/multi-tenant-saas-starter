using SharedKernel;

namespace Billing.Contracts.PriceChanges;

/// <summary>
/// A tenant that pays for a plan by card was told that the plan's price changes (B13, 12.12): from its first period after
/// <paramref name="MovesAt"/> it pays the new prices. Published through the outbox with the tenant's and the plan's names of that moment,
/// which the Activity log keeps (K3); the tenant's id travels in the envelope too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="Yearly">Whether the tenant pays yearly, so its prices are the yearly ones.</param>
/// <param name="BillingEmail">The tenant's billing email, which billing's emails go to besides its members (B14); none without one.</param>
/// <param name="BillingEmailSetBy">The user id of the member who set the billing email, whose language and time zone its email takes.</param>
[MessageName("price-change-announced")]
public sealed record PriceChangeAnnouncedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid PlanId,
    string PlanName,
    string Currency,
    bool Yearly,
    PriceChangePricesRecord OldPrices,
    PriceChangePricesRecord NewPrices,
    DateTimeOffset MovesAt,
    string? BillingEmail = null,
    Guid? BillingEmailSetBy = null);
