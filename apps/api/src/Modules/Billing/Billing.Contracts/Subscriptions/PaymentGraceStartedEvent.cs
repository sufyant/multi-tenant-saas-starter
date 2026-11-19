using SharedKernel;

namespace Billing.Contracts.Subscriptions;

/// <summary>
/// The provider's retries of the tenant's unpaid invoice are over, and billing's grace started (B11): the tenant keeps its plan until
/// <paramref name="GraceEndsAt"/>, then moves to the default plan. It is the last warning to the members who manage billing (13.19); the
/// Activity log records no entry of it. Published through the outbox in the tenant's transaction, so the tenant's id travels in the envelope
/// too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="Attempts">How many of the provider's attempts failed.</param>
/// <param name="FailedAt">When the first attempt failed.</param>
/// <param name="CardBrand">The brand of the card it tried, as the provider names it (<c>visa</c>); none when it named no card.</param>
/// <param name="CardLast4">The card's last four digits; none when it named no card.</param>
/// <param name="BillingEmail">The tenant's billing email, which billing's emails go to besides its members (B14); none without one.</param>
/// <param name="BillingEmailSetBy">The user id of the member who set the billing email, whose language and time zone its email takes.</param>
[MessageName("payment-grace-started")]
public sealed record PaymentGraceStartedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    DateTimeOffset GraceEndsAt,
    int Attempts,
    DateTimeOffset FailedAt,
    string? CardBrand,
    string? CardLast4,
    string? BillingEmail = null,
    Guid? BillingEmailSetBy = null);
