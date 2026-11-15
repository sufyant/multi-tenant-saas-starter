using SharedKernel;

namespace Billing.Contracts.Subscriptions;

/// <summary>
/// A payment of the tenant's subscription failed for the first time on an invoice (B11): the provider retries it until
/// <paramref name="RetriesUntil"/>. Published through the outbox with the tenant's name of that moment, which the Activity log keeps (K3,
/// 12.12); the tenant's id travels in the envelope too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="AmountDue">The amount due without tax, in the currency's minor unit.</param>
[MessageName("payment-failed")]
public sealed record PaymentFailedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    long AmountDue,
    string Currency,
    DateTimeOffset RetriesUntil);
