namespace Billing.Application.Provider;

/// <summary>
/// The subscription's open invoice whose payment failed, as the provider holds it (B11): the amount due without tax, in the currency's minor
/// unit, when it was due, how many attempts failed, and when the provider tries again, none when its retries are over; and the brand and the
/// last four digits of the card it tried, when the provider names one. It travels in the sync's messages too.
/// </summary>
public sealed record ProviderPayment(
    string InvoiceId,
    long AmountDue,
    string Currency,
    DateTimeOffset FailedAt,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    string? CardBrand,
    string? CardLast4);
