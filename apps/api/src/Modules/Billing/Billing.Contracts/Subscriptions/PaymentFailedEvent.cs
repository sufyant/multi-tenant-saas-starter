using SharedKernel;

namespace Billing.Contracts.Subscriptions;

/// <summary>
/// A payment of the tenant's subscription failed for the first time on an invoice (B11): the provider retries it until
/// <paramref name="RetriesUntil"/>. Published through the outbox with the tenant's name of that moment, which the Activity log keeps (K3,
/// 12.12); the tenant's id travels in the envelope too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="AmountDue">The amount due without tax, in the currency's minor unit.</param>
/// <param name="FailedAt">When the provider's first attempt failed; none in a message of the version before part 10.</param>
/// <param name="NextAttemptAt">When the provider tries again; none when it does not, or in a message of the version before part 10.</param>
/// <param name="CardBrand">The brand of the card it tried, as the provider names it (<c>visa</c>); none when it named no card.</param>
/// <param name="CardLast4">The card's last four digits; none when it named no card.</param>
[MessageName("payment-failed")]
public sealed record PaymentFailedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    long AmountDue,
    string Currency,
    DateTimeOffset RetriesUntil,
    DateTimeOffset? FailedAt = null,
    DateTimeOffset? NextAttemptAt = null,
    string? CardBrand = null,
    string? CardLast4 = null);
